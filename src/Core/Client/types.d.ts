declare module Bll {
    interface Terminal {
        name: string;
        replicas?: number | null;
        cnameMaps: CnameMap[];
    }

    interface CnameMap {
        name: string;
        port: number;
    }

    interface CreateTerminalRequest {
        name: string;
        image: string;
        password?: string;
        shell?: string;
        cnames?: string;
        entrypoint?: string;
        environmentVariables?: string;
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