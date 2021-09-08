import { CaretPosition, TokenPosition } from "./types";
import { parseRql } from "./parser";
import { computeTokenPosition } from "./caretPosition";
import { RqlQueryVisitor } from "./rqlQueryVisitor";
import { RqlParser } from "./generated/RqlParser";
import { ParseTree } from "antlr4ts/tree/ParseTree";
import { CodeCompletionCore, SymbolTable } from "antlr4-c3";
import { TerminalNode } from "antlr4ts/tree/TerminalNode";
import { CandidatesCollection } from "antlr4-c3/out/src/CodeCompletionCore";

function filterTokens(text: string, candidates: string[]) {
    if (text.trim().length == 0) {
        return candidates;
    } else {
        return candidates.filter(c => c.toLowerCase().startsWith(text.toLowerCase()));
    }
}

const ignoredTokens: number[] = [
    RqlParser.OP_PAR,
    RqlParser.CL_PAR,
    RqlParser.OP_Q,
    RqlParser.CL_Q
];


function completeKeywords(position: TokenPosition, candidates: CandidatesCollection, parser: RqlParser): autoCompleteWordList[] {
    
    //TODO: review me!
    
    const completions: autoCompleteWordList[] = [];
    
    const tokens: string[] = [];
    candidates.tokens.forEach((_, k) => {
        const symbolicName = parser.vocabulary.getSymbolicName(k);
        if (symbolicName) {
            tokens.push(symbolicName.toLowerCase());
        }
    });

    const isIgnoredToken =
        position.context instanceof TerminalNode &&
        ignoredTokens.indexOf(position.context.symbol.type) >= 0;

    const textToMatch = isIgnoredToken ? '' : position.text;

    completions.push(...filterTokens(textToMatch, tokens).map(x => ({
        caption: x,
        value: x,
        score: 100,
        meta: "keyword"
    })));
    
    return completions;
}

export async function getSuggestionsForParseTree(
    parser: RqlParser, parseTree: ParseTree, symbolTableFn: () => SymbolTable, position: TokenPosition): Promise<autoCompleteWordList[]> {
    const core = new CodeCompletionCore(parser);

    core.ignoredTokens = new Set(ignoredTokens);

    core.preferredRules = new Set([
        RqlParser.RULE_indexName,
        RqlParser.RULE_collectionName,
        RqlParser.RULE_identifiersNames,
        RqlParser.RULE_specialFunctionName
    ]);

    const candidates = core.collectCandidates(position.index);

    if (candidates.rules.has(RqlParser.RULE_collectionName)) {
        //TODO: collection name
    }
    console.log(candidates); //TODO:
    
    const completions: autoCompleteWordList[] = [];

    completions.push(...completeKeywords(position, candidates, parser));
    
    //TODO: allow to supply list with additional completion providers

    return completions;
}

function handleEmptyQuery(): autoCompleteWordList[] {
    return [
        {
            value: "from ",
            score: 1000,
            caption: "from",
            meta: "from collection"
        },
        {
            value: "from index ",
            score: 1000,
            caption: "from index",
            meta: "from index"
        }
    ]
}

export async function handleAutoComplete(input: string, caret: CaretPosition): Promise<autoCompleteWordList[]> {
    const { parseTree, tokenStream, parser } = parseRql(input);

    const position = computeTokenPosition(parseTree, tokenStream, caret);

    if (!position) {
        return handleEmptyQuery();
    }
    return getSuggestionsForParseTree(
        parser, parseTree, () => new RqlQueryVisitor().visit(parseTree), position);
}
