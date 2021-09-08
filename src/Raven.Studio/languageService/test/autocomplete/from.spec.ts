import { autocomplete } from "../autocompleteUtils";

describe("can complete from", function () {

    it("empty", async function () {
        const suggestions = await autocomplete(" |");
        
        
        console.log(suggestions); //TODO:
    });
    
    it("partial", async function () {
        const suggestions = await autocomplete("fr|");
        console.log(suggestions);
    });

    it("can complete collection name with where", async function () {
        const suggestions = await autocomplete("from Ord| where ");
        console.log(suggestions);
    });
    
    describe("from index", function () {
        it("can complete index name", async function () {
            const suggestions = await autocomplete(`from
             index "Products/ByName" where O|`);
            console.log(suggestions); //TODO:
        });
    });
})
