import React, { useEffect, useState } from "react";
import activeDatabaseTracker from "common/shell/activeDatabaseTracker";
import getDatabasesCommand from "commands/resources/getDatabasesCommand";

import DatabasesInfo = Raven.Client.ServerWide.Operations.DatabasesInfo;
export function Button(props: any) {
    
    const [dbs, setDbs] = useState<DatabasesInfo>();
    
    useEffect(() => {
        console.log("in effect");
        return () => {
            console.log("dispose effect");
        }
    }, []);
    
    useEffect(() => {
        new getDatabasesCommand()
            .execute()
            .done(loadedDbs => {
                setDbs(loadedDbs);
            });
    }, []);
    
    useEffect(() => {
        const sub = activeDatabaseTracker.default.database.subscribe(d => {
            console.log("a", d);
        });
        
        return () => sub.dispose();
    })
    
    const [ counter, setCounter ] = useState<number>(0);
    
    return (
        <div>
            <h1>Greeting from React, react counter = {counter}, knockout counter = {props.counter}</h1>
            <button type="button" className="btn btn-default" onClick={() => setCounter(counter + 1)}>Inc React Counter</button>
            
            <ul>
                {dbs && dbs.Databases.map(x => {
                    return (
                        <li key={x.Name}>{x.Name}</li>
                    )
                })}
            </ul>
        </div>
    );
}
