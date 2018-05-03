declare module "*.html" {
    const content: string;
    export default content;
}

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
        password: string;
        shell: string;
    }
}