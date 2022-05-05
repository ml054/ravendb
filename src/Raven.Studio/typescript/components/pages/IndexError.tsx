import React, { useEffect, useLayoutEffect, useRef } from "react";
import virtualGridController from "widgets/virtualGrid/virtualGridController";
import textColumn from "widgets/virtualGrid/columns/textColumn";


export function IndexError() {
    
    const koNode = useRef<HTMLDivElement>();
    
    useLayoutEffect(() => {
        koNode.current.innerHTML = ` <virtual-grid data-bind="attr: { class: 'resizable ' }"
                                                  params="controller: gridController">
                                    </virtual-grid>`;
        
        const vm = {
            gridController: ko.observable<virtualGridController<IndexErrorPerDocument>>()
        };
        
        ko.applyBindings(vm, koNode.current);
        
        requestAnimationFrame(() => {
            const grid = vm.gridController();
            console.log("GRID = ", grid);
            if (grid) {
                grid.headerVisible(true);
                grid.init(() => getIndexErrors(), () =>
                    [
                        new textColumn<any>(grid, x => x.a, "Action", "10%", {
                            sortable: "string"
                        }),
                        new textColumn<any>(grid, x => x.b, "Error", "15%", {
                            sortable: "string"
                        })
                    ]
                );
            }
        })
        
        

        return () => {
            ko.cleanNode(koNode.current);
        }
        
    }, []);
    // @ts-ignore
    
    return (
        <div>
            <h1>works!</h1>
            <div ref={koNode} style={{ height: "400px" }}>
            </div>
        </div>
    );
}


function getIndexErrors(): JQueryPromise<pagedResult<any>> {
    const task = $.Deferred<any>();
    
    task.resolve({
        items: [
            {
                a: "5",
                b: "4"
            }
        ],
        totalResultCount: 1
    })
    
    return task;
}
