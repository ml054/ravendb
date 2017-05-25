﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Raven.Client;
using Raven.Client.Documents.Exceptions.Compilation;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Transformers;
using Raven.Server.Documents.Indexes.Static.Roslyn;
using Raven.Server.Documents.Indexes.Static.Roslyn.Rewriters;
using Raven.Server.Documents.Indexes.Static.Roslyn.Rewriters.ReduceIndex;
using Raven.Server.Documents.Transformers;
using System.Collections;

namespace Raven.Server.Documents.Indexes.Static
{
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
    public static class IndexAndTransformerCompiler
    {
        private static readonly bool EnableDebugging = true; // for debugging purposes

        private const string IndexNamespace = "Raven.Server.Documents.Indexes.Static.Generated";

        private const string TransformerNamespace = "Raven.Server.Documents.Transformers.Generated";

        private const string IndexExtension = ".index";

        private const string TransformerExtension = ".transformer";

        private static readonly UsingDirectiveSyntax[] Usings =
        {
            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("System.Collections")),
            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("System.Collections.Generic")),
            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("System.Linq")),
            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("System.Text.RegularExpressions")),

            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("Lucene.Net.Documents")),

            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("Raven.Server.Documents.Indexes.Static")),
            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("Raven.Server.Documents.Indexes.Static.Linq")),
            SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("Raven.Server.Documents.Indexes.Static.Extensions"))
        };

        private static readonly MetadataReference[] References =
        {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ExpressionType).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IndexAndTransformerCompiler).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DynamicAttribute).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(BoostedValue).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Lucene.Net.Documents.Document).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location),
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("Microsoft.CSharp")).Location),
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("mscorlib")).Location),
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Collections")).Location),
            MetadataReference.CreateFromFile(typeof(Regex).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Uri).GetTypeInfo().Assembly.Location)
        };

        public static TransformerBase Compile(TransformerDefinition definition)
        {
            var cSharpSafeName = GetCSharpSafeName(definition.Name, isIndex: false);

            var @class = CreateClass(cSharpSafeName, definition);

            var compilationResult = CompileInternal(definition.Name, cSharpSafeName, @class, isIndex: false);
            var type = compilationResult.Type;

            var transformer = (TransformerBase)Activator.CreateInstance(type);
            transformer.Source = compilationResult.Code;

            return transformer;
        }

        public static StaticIndexBase Compile(IndexDefinition definition)
        {
            var cSharpSafeName = GetCSharpSafeName(definition.Name, isIndex: true);

            var @class = CreateClass(cSharpSafeName, definition);

            var compilationResult = CompileInternal(definition.Name, cSharpSafeName, @class, isIndex: true);
            var type = compilationResult.Type;

            var index = (StaticIndexBase)Activator.CreateInstance(type);
            index.Source = compilationResult.Code;

            return index;
        }

        private static CompilationResult CompileInternal(string originalName, string cSharpSafeName, MemberDeclarationSyntax @class, bool isIndex)
        {
            var name = cSharpSafeName + "." + Guid.NewGuid() + (isIndex ? IndexExtension : TransformerExtension);

            var @namespace = RoslynHelper.CreateNamespace(isIndex ? IndexNamespace : TransformerNamespace)
                .WithMembers(SyntaxFactory.SingletonList(@class));

            var compilationUnit = SyntaxFactory.CompilationUnit()
                .WithUsings(RoslynHelper.CreateUsings(Usings))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(@namespace))
                .NormalizeWhitespace();

            var formatedCompilationUnit = compilationUnit; //Formatter.Format(compilationUnit, new AdhocWorkspace()); // TODO [ppekrol] for some reason formatedCompilationUnit.SyntaxTree does not work

            string sourceFile = null;

            if (EnableDebugging)
            {
                sourceFile = Path.Combine(Path.GetTempPath(), name + ".cs");
                File.WriteAllText(sourceFile, formatedCompilationUnit.ToFullString(), Encoding.UTF8);
            }

            var compilation = CSharpCompilation.Create(
                assemblyName: name + ".dll",
                syntaxTrees: new[]
                {
                    EnableDebugging ?
                    SyntaxFactory.ParseSyntaxTree(File.ReadAllText(sourceFile), path: sourceFile, encoding: Encoding.UTF8) :
                    SyntaxFactory.ParseSyntaxTree(formatedCompilationUnit.ToFullString())
                },
                references: References,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release)
                );

            var code = formatedCompilationUnit.SyntaxTree.ToString();

            var asm = new MemoryStream();
            var pdb = EnableDebugging ? new MemoryStream() : null;

            var result = compilation.Emit(asm, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

            if (result.Success == false)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics
                    .Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                var sb = new StringBuilder();
                sb.AppendLine($"Failed to compile {(isIndex ? "index" : "transformer")} {originalName}");
                sb.AppendLine();
                sb.AppendLine(code);
                sb.AppendLine();

                foreach (var diagnostic in failures)
                    sb.AppendLine(diagnostic.ToString());

                if (isIndex)
                    throw new IndexCompilationException(sb.ToString());

                throw new TransformerCompilationException(sb.ToString());
            }

            asm.Position = 0;

            Assembly assembly;

            if (EnableDebugging)
            {
                pdb.Position = 0;
                assembly = AssemblyLoadContext.Default.LoadFromStream(asm, pdb);
            }
            else
            {
                assembly = AssemblyLoadContext.Default.LoadFromStream(asm);
            }

            return new CompilationResult
            {
                Code = code,
                Type = assembly.GetType($"{(isIndex ? IndexNamespace : TransformerNamespace)}.{cSharpSafeName}")
            };
        }

        private static MemberDeclarationSyntax CreateClass(string name, TransformerDefinition definition)
        {
            var statements = new List<StatementSyntax>();

            var methodDetector = new MethodDetectorRewriter();
            statements.Add(HandleTransformResults(definition.TransformResults, methodDetector));

            var methods = methodDetector.Methods;

            if (methods.HasGroupBy)
                statements.Add(RoslynHelper.This(nameof(TransformerBase.HasGroupBy)).Assign(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)).AsExpressionStatement());

            if (methods.HasLoadDocument)
                statements.Add(RoslynHelper.This(nameof(TransformerBase.HasLoadDocument)).Assign(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)).AsExpressionStatement());

            if (methods.HasTransformWith)
                statements.Add(RoslynHelper.This(nameof(TransformerBase.HasTransformWith)).Assign(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)).AsExpressionStatement());

            if (methods.HasInclude)
                statements.Add(RoslynHelper.This(nameof(TransformerBase.HasInclude)).Assign(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)).AsExpressionStatement());

            var ctor = RoslynHelper.PublicCtor(name).AddBodyStatements(statements.ToArray());

            return RoslynHelper.PublicClass(name)
                .WithBaseClass<TransformerBase>()
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(ctor));
        }

        private static MemberDeclarationSyntax CreateClass(string name, IndexDefinition definition)
        {
            var statements = new List<StatementSyntax>();
            var maps = definition.Maps.ToList();
            var fieldNamesValidator = new FieldNamesValidator();
            var methodDetector = new MethodDetectorRewriter();
            var members = new SyntaxList<MemberDeclarationSyntax>();

            for (var i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                statements.AddRange(HandleMap(map, fieldNamesValidator, methodDetector, ref members));
            }

            if (string.IsNullOrWhiteSpace(definition.Reduce) == false)
            {
                string[] groupByFields;
                statements.Add(HandleReduce(definition.Reduce, fieldNamesValidator, methodDetector, out groupByFields));

                var groupByFieldsArray = GetArrayCreationExpression(groupByFields);
                statements.Add(RoslynHelper.This(nameof(StaticIndexBase.GroupByFields)).Assign(groupByFieldsArray).AsExpressionStatement());
            }

            var outputFieldsArray = GetArrayCreationExpression(fieldNamesValidator.Fields);
            statements.Add(RoslynHelper.This(nameof(StaticIndexBase.OutputFields)).Assign(outputFieldsArray).AsExpressionStatement());

            var methods = methodDetector.Methods;

            if (methods.HasCreateField)
                statements.Add(RoslynHelper.This(nameof(StaticIndexBase.HasDynamicFields)).Assign(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)).AsExpressionStatement());

            if (methods.HasBoost)
                statements.Add(RoslynHelper.This(nameof(StaticIndexBase.HasBoostedFields)).Assign(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)).AsExpressionStatement());

            var ctor = RoslynHelper.PublicCtor(name)
                .AddBodyStatements(statements.ToArray());


            return RoslynHelper.PublicClass(name)
                .WithBaseClass<StaticIndexBase>()
                .WithMembers(members.Add(ctor));
        }

        private static StatementSyntax HandleTransformResults(string transformResults, MethodDetectorRewriter methodsDetector)
        {
            try
            {
                transformResults = NormalizeFunction(transformResults);
                var expression = SyntaxFactory.ParseExpression(transformResults).NormalizeWhitespace();
                methodsDetector.Visit(expression);

                var queryExpression = expression as QueryExpressionSyntax;
                if (queryExpression != null)
                    return HandleSyntaxInTransformResults(new TransformFunctionProcessor(SelectManyRewriter.QuerySyntax), queryExpression);

                var invocationExpression = expression as InvocationExpressionSyntax;
                if (invocationExpression != null)
                    return HandleSyntaxInTransformResults(new TransformFunctionProcessor(SelectManyRewriter.MethodSyntax), invocationExpression);

                throw new InvalidOperationException("Not supported expression type.");
            }
            catch (Exception ex)
            {
                throw new TransformerCompilationException(ex.Message, ex)
                {
                    TransformerDefinitionProperty = nameof(TransformerDefinition.TransformResults),
                    ProblematicText = transformResults
                };
            }
        }

        private static List<StatementSyntax> HandleMap(string map, FieldNamesValidator fieldNamesValidator, MethodDetectorRewriter methodsDetector,
            ref SyntaxList<MemberDeclarationSyntax> members)
        {
            try
            {
                map = NormalizeFunction(map);
                var expression = SyntaxFactory.ParseExpression(map).NormalizeWhitespace();

                fieldNamesValidator.Validate(map, expression);
                methodsDetector.Visit(expression);

                var queryExpression = expression as QueryExpressionSyntax;
                if (queryExpression != null)
                    return HandleSyntaxInMap(fieldNamesValidator, new MapFunctionProcessor(CollectionNameRetriever.QuerySyntax, SelectManyRewriter.QuerySyntax), queryExpression, ref members);
                var invocationExpression = expression as InvocationExpressionSyntax;
                if (invocationExpression != null)
                    return HandleSyntaxInMap(fieldNamesValidator, new MapFunctionProcessor(CollectionNameRetriever.MethodSyntax, SelectManyRewriter.MethodSyntax), invocationExpression, ref members);

                throw new InvalidOperationException("Not supported expression type.");
            }
            catch (Exception ex)
            {
                throw new IndexCompilationException(ex.Message, ex)
                {
                    IndexDefinitionProperty = nameof(IndexDefinition.Maps),
                    ProblematicText = map
                };
            }
        }

        private static StatementSyntax HandleReduce(string reduce, FieldNamesValidator fieldNamesValidator, MethodDetectorRewriter methodsDetector, out string[] groupByFields)
        {
            try
            {
                reduce = NormalizeFunction(reduce);
                var expression = SyntaxFactory.ParseExpression(reduce).NormalizeWhitespace();
                fieldNamesValidator?.Validate(reduce, expression);
                methodsDetector.Visit(expression);
                var queryExpression = expression as QueryExpressionSyntax;
                if (queryExpression != null)
                {
                    return
                        HandleSyntaxInReduce(
                            new ReduceFunctionProcessor(
                                ResultsVariableNameRewriter.QuerySyntax,
                                GroupByFieldsRetriever.QuerySyntax,
                                SelectManyRewriter.QuerySyntax),
                            MethodsInGroupByValidator.QuerySyntaxValidator,
                            queryExpression, out groupByFields);
                }

                var invocationExpression = expression as InvocationExpressionSyntax;
                if (invocationExpression != null)
                {
                    return
                        HandleSyntaxInReduce(
                            new ReduceFunctionProcessor(
                                ResultsVariableNameRewriter.MethodSyntax,
                                GroupByFieldsRetriever.MethodSyntax,
                                SelectManyRewriter.MethodSyntax),
                            MethodsInGroupByValidator.MethodSyntaxValidator,
                            invocationExpression, out groupByFields);
                }

                throw new InvalidOperationException("Not supported expression type.");
            }
            catch (Exception ex)
            {
                throw new IndexCompilationException(ex.Message, ex)
                {
                    IndexDefinitionProperty = nameof(IndexDefinition.Reduce),
                    ProblematicText = reduce
                };
            }
        }

        private static StatementSyntax HandleSyntaxInTransformResults(TransformFunctionProcessor processor, ExpressionSyntax expression)
        {
            var rewrittenExpression = (CSharpSyntaxNode)processor.Visit(expression);

            var indexingFunction = SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier("results")), rewrittenExpression);

            return RoslynHelper
                .This(nameof(TransformerBase.TransformResults))
                .Assign(indexingFunction)
                .AsExpressionStatement();
        }

        private static List<StatementSyntax> HandleSyntaxInMap(FieldNamesValidator fieldValidator, MapFunctionProcessor mapRewriter, ExpressionSyntax expression,
            ref SyntaxList<MemberDeclarationSyntax> members)
        {
            var rewrittenExpression = (CSharpSyntaxNode)mapRewriter.Visit(expression);

            var optimized = new RavenLinqOptimizer(fieldValidator).Visit(new RavenLinqPrettifier().Visit(rewrittenExpression))
                as StatementSyntax;

            var collectionName = string.IsNullOrWhiteSpace(mapRewriter.CollectionName) ? Constants.Documents.Collections.AllDocumentsCollection : mapRewriter.CollectionName;

            var collection = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(collectionName));
            var results = new List<StatementSyntax>();

            if (optimized != null)
            {
                var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName("IEnumerable"), SyntaxFactory.Identifier("Map_" + members.Count))
                    .WithParameterList(
                        SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("docs"))
                                .WithType(
                                    SyntaxFactory.GenericName("IEnumerable")
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(SyntaxFactory.IdentifierName("dynamic"))
                                            )
                                        )
                                )
                            ))
                    )
                    .WithBody(SyntaxFactory.Block().AddStatements(optimized));

                members = members.Add(method);

                results.Add(RoslynHelper.This(nameof(StaticIndexBase.AddMap)).Invoke(collection, RoslynHelper.This(method.Identifier.Text)).AsExpressionStatement()); // this.AddMap("Users", docs => from doc in docs ... )

            }
            else
            {
                var indexingFunction = SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier("docs")), rewrittenExpression);

                results.Add(RoslynHelper.This(nameof(StaticIndexBase.AddMap)).Invoke(collection, indexingFunction).AsExpressionStatement()); // this.AddMap("Users", docs => from doc in docs ... )
            }

            if (mapRewriter.ReferencedCollections != null)
            {
                foreach (var referencedCollection in mapRewriter.ReferencedCollections)
                {
                    var rc = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(referencedCollection));
                    results.Add(RoslynHelper.This(nameof(StaticIndexBase.AddReferencedCollection)).Invoke(collection, rc).AsExpressionStatement());
                }
            }

            return results;
        }

        private static StatementSyntax HandleSyntaxInReduce(ReduceFunctionProcessor reduceFunctionProcessor, MethodsInGroupByValidator methodsInGroupByValidator,
            ExpressionSyntax expression, out string[] groupByFields)
        {

            var rewrittenExpression = (CSharpSyntaxNode)reduceFunctionProcessor.Visit(expression);

            var reducingFunction =
                SyntaxFactory.SimpleLambdaExpression(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier(ResultsVariableNameRewriter.ResultsVariable)),
                    rewrittenExpression);

            methodsInGroupByValidator.Start(expression);

            groupByFields = reduceFunctionProcessor.GroupByFields;

            return RoslynHelper.This(nameof(StaticIndexBase.Reduce)).Assign(reducingFunction).AsExpressionStatement();
        }

        private static ArrayCreationExpressionSyntax GetArrayCreationExpression(IEnumerable<string> items)
        {
            return SyntaxFactory.ArrayCreationExpression(SyntaxFactory.ArrayType(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)))
                    .WithRankSpecifiers(
                        SyntaxFactory.SingletonList<ArrayRankSpecifierSyntax>(
                            SyntaxFactory.ArrayRankSpecifier(
                                    SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                                        SyntaxFactory.OmittedArraySizeExpression()
                                            .WithOmittedArraySizeExpressionToken(
                                                SyntaxFactory.Token(SyntaxKind.OmittedArraySizeExpressionToken))))
                                .WithOpenBracketToken(SyntaxFactory.Token(SyntaxKind.OpenBracketToken))
                                .WithCloseBracketToken(SyntaxFactory.Token(SyntaxKind.CloseBracketToken)))))
                .WithNewKeyword(SyntaxFactory.Token(SyntaxKind.NewKeyword))
                .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                        SyntaxFactory.SeparatedList<ExpressionSyntax>(items.Select(
                            x =>
                                SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(x)))))
                    .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)));
        }

        private static string GetCSharpSafeName(string name, bool isIndex)
        {
            return $"{(isIndex ? "Index" : "Transformer")}_{Regex.Replace(name, @"[^\w\d]", "_")}";
        }

        private static string NormalizeFunction(string function)
        {
            return function?.Trim().TrimEnd(';');
        }

        private class CompilationResult
        {
            public Type Type { get; set; }
            public string Code { get; set; }
        }

        public class IndexAndTransformerMethods
        {
            public bool HasLoadDocument { get; set; }

            public bool HasTransformWith { get; set; }

            public bool HasGroupBy { get; set; }

            public bool HasInclude { get; set; }

            public bool HasCreateField { get; set; }

            public bool HasBoost { get; set; }
        }
    }
}