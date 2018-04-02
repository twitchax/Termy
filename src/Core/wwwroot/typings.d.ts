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

    interface Image {
        repository:string;
        tag: string;
        imageid: string; 
        created: string;
        size: string;
    }

    interface CreateTerminalRequest {
        name: string;
        image: string;
        tag: string;
        rootPassword: string;
        shell: string;
        dockerUsername: string;
        dockerPassword: string;
    }
}