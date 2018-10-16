export interface Request {
    method?: string;
    url: string;
    headers?: any;
    body?: object;
}

function buildErrorMessage(xhr: XMLHttpRequest) {
    return `Code: ${xhr.status}.\nStatus: ${xhr.statusText}.\nResponse: ${xhr.response}`;
}

export function request<T>(obj: Request) {
    return new Promise<T>((resolve, reject) => {
        let xhr = new XMLHttpRequest();
        xhr.open(obj.method || 'GET', obj.url, true);

        if (obj.headers) {
            let headers = (obj.headers || {} as any);
            Object.keys(headers).forEach(key => {
                xhr.setRequestHeader(key, headers[key] as string);
            });
        }
        xhr.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
        xhr.setRequestHeader('Accept', 'application/json');

        xhr.onload = () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    resolve(JSON.parse(xhr.responseText));
                    return;
                } catch(e) {
                    resolve();
                    return;
                }
                
            } else {
                reject(buildErrorMessage(xhr));
                return;
            }
        };
        xhr.onerror = () => reject(buildErrorMessage(xhr));

        xhr.send(JSON.stringify(obj.body));
    });
}

export class BackgroundWorker<T> {
    private _worker: Worker;
    private _hasOnMessage = false;
    private _hasOnError = false;

    constructor(worker: Worker) {
        this._worker = worker;
    }

    static start<T>(workerFunc: () => void) {
        return new BackgroundWorker<T>(BackgroundWorker._stringToWorker(`(${workerFunc.toString()})();`));
    }

    onMessage<T>(callback: (message: T) => void) {
        if(this._hasOnMessage)
        throw 'Only one instance of onMessage allowed.';

        this._worker.onmessage = (ev) => {
            callback(ev.data as T);
        };

        this._hasOnMessage = true;
        
        return this;
    }

    onError(callback: (message: any) => void) {
        if(this._hasOnError)
            throw 'Only one instance of onError allowed.';

        this._worker.onerror = (ev) => {
            callback(ev.error);
        };

        this._hasOnError = true;
        
        return this;
    }

    private static _stringToWorker(src: string) {
        var response = src;
        var blob = new Blob([response], {type: 'application/javascript'});
        return new Worker(URL.createObjectURL(blob));
    };
}