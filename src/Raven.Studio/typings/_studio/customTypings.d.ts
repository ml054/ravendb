/// QRCode
/// <reference types="lodash" />

declare const QRCode: any;

declare var require: any;

/// Sortable
declare const Sortable: any;

declare const _: LoDashStatic;


///
/// JSZip
///

declare const JSZipUtils: {
    getBinaryContent: (url: string, handler: (error: any, data: any) => void) => void;
};

declare module "jszip-utils" {
    export = JSZipUtils;
}

declare module "node-forge" {
    namespace pki {
        function certificateFromPem(pem: string): any;
        function certificateToAsn1(cert: pki.Certificate): any;
    }
}

/// Cola.js

declare module 'cola' {
    export = cola;
}

/// Favico
///
/// Using *any* as official typings are broken

declare const Favico: any;

///
/// jQuery: 
///   - selectpicker 
///   - multiselect
///   - highlight
///   - fullscreen
///

interface JQuery {

    selectpicker(): void;

    multiselect(action?: string): void;
    multiselect(options?: any): void;

    highlight(): void;

    toggleFullScreen(): void;
    fullScreen(arg: boolean): void;
    fullScreen(): boolean;
}


///
/// jwerty
///


interface JwertyStatic {
    key(keyCombination: string, handler: (event: KeyboardEvent, keyCombination: string) => any, context?: any, selector?: string): JwertySubscription;
}

interface JwertySubscription {
    unbind(): void;
}

declare var jwerty: JwertyStatic;

///
/// Ace
///
declare module "ace/ace" {
    export = ace;
}

declare namespace AceAjax {
    export interface IEditSession {
        widgetManager: WidgetManager;
        off(event: string, fn: (e: any) => any): void;
        setFoldStyle(style: "manual" | "markbegin" | "markbeginend"): void;
        getFoldWidgetRange: (row: number) => Range;
    }
    
    export interface WidgetManager {
        addLineWidget: (widget: any) => void;
        removeLineWidget: (wigdet: any) => void;
    }
    
    export interface VirtualRenderer {
        layerConfig: VirtualRendererConfig;
    }
    
    export interface VirtualRendererConfig {
        lineHeight: number;
    }
    
    export interface TextMode {
        $id: string;
    }
}

interface CSS {
    escape(input: string): string;
}

interface KnockoutObservable<T> {
    throttle(throttleTimeInMs: number): KnockoutObservable<T>;
    distinctUntilChanged(): KnockoutObservable<T>;
    toggle(): KnockoutObservable<T>;
}

interface KnockoutStatic {
    DirtyFlag: {
        new (inputs: any[], isInitiallyDirty?: boolean, hashFunction?: (obj: any) => string): () => DirtyFlag;
    }
}

interface DirtyFlag {
    isDirty(): boolean;
    reset(): void;
    forceDirty(): void;
}

interface Cronstrue {
    toString(string: string): string;
}

declare var cronstrue: Cronstrue;

interface Spinner {
    stop() :void;
    spin(): Spinner;
    spin(p1: HTMLElement): Spinner;
    el: Node;
}

declare var Spinner: {
    new (spinnerOptions: {
        lines: number; length: number; width: number; radius: number; scale: number; corners: number;
        color: any; opacity: number; rotate: number; direction: number; speed: number; trail: number; fps: number; zIndex: number;
        className: string; top: string; left: string; shadow: boolean; hwaccel: boolean; position: string;
    }): Spinner;
}

interface Storage {
    getObject: (string: string) => any;
    setObject: (key: string, value: any) => void;
}

interface DurandalRouteConfiguration {
    tooltip?: string;
    dynamicHash?: KnockoutObservable<string> | (() => string);
    tabName?: string;
    moduleId: Function | string;
}

declare module AceAjax {
    interface IEditSession {
        foldAll(): void;
    }

    interface TokenInfo {
        index: number;
        start: number;
        type: string;
    }

    interface TokenIterator {
        $tokenIndex: number;
    }

    interface TextMode {
        prefixRegexps: RegExp[];
        $highlightRules: HighlightRules;
    }

    interface Selection {
        lead: Anchor;
    }

    interface Anchor {
        column: number;
        row: number;
    }

    interface HighlightRules {
    }

    interface RqlHighlightRules extends HighlightRules {
        clausesKeywords: string[];
        clauseAppendKeywords: string[];
        binaryOperations: string[];
        whereFunctions: string[];
    }
}

interface DurandalAppModule {
    showDialog(please_use_app_showBootstrapDialog_instead: any): void;

    showMessage(please_use_app_showBootstrapMessage_instead: any): void;

    showBootstrapDialog(obj: any, activationData?: any): JQueryPromise<any>;

    showBootstrapMessage(message: string, title?: string, options?: string[], autoclose?: boolean, settings?: Object): DurandalPromise<string>;
}
