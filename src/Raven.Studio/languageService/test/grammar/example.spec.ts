import { parseRql } from "../../src/parser";
import { CollectionByNameContext } from "../../src/generated/RqlParser";


describe("Example test", function() {
    it("from collection", function() {
        const { parseTree, parser } = parseRql("from Orders");
        
        expect(parser.numberOfSyntaxErrors)
            .toEqual(0);
        
        const from = parseTree.fromStatement();
        
        expect(from)
            .toBeInstanceOf(CollectionByNameContext);
        
        const collectionByName = from as CollectionByNameContext;
        expect(collectionByName.collectionName().text)
            .toEqual("Orders");
    });
});
