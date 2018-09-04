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
};