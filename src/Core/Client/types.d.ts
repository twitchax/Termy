declare module Bll {
    interface Terminal {
        name: string;
        type: string;
        clusterip: string;
        externalip: string;
        ports: string;
        age: string;
    }

    interface CreateTerminalRequest {
        name: string;
        image: string;
        password?: string;
        shell?: string;
        port?: number;
        command?: string;
    }
}

declare function require(name: string): any;