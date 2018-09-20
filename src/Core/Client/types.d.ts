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
        cnames?: string;
        //port?: number;
        command?: string;
    }

    interface NodeStat {
        name: string;
        cpuCores: number;
        cpuPercent: number;
        memoryBytes: number;
        memoryPercent: number;
        time: string;
    }
}

declare function require(name: string): any;