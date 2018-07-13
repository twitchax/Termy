import { PolymerElement } from '@polymer/polymer/polymer-element';
import * as request from 'browser-request';

import '@polymer/paper-card/paper-card';
import '@polymer/iron-list/iron-list';
import '@polymer/paper-item/paper-item';
import '@polymer/paper-button/paper-button';
import '@polymer/paper-toast/paper-toast';
import '@polymer/paper-input/paper-input';
import '@polymer/paper-input/paper-textarea';
import '@polymer/paper-spinner/paper-spinner';

import * as view from './app.template.html';

export class MyApp extends PolymerElement {
    static get properties() {
        return {
            terminals: Array, 
        };
    }

    terminals!: Bll.Terminal[];

    static get template() {
        return view;
    }

    constructor() {
        super();
        
        this.setup();
    }

    private setup() {
        request('/api/terminal', (err, res, body) => {
            if(!this.checkResponse(err, res, body)) return;
            
            this.terminals = JSON.parse(body);
        });
    }

    private createTerminal() {
        var req = {} as Bll.CreateTerminalRequest;
        req.name = (this as any).$.terminalName.value;
        req.image = (this as any).$.terminalImage.value;
        req.port = (this as any).$.terminalPort.value;
        req.password = (this as any).$.terminalPassword.value;
        req.shell = (this as any).$.terminalShell.value;
        req.command = (this as any).$.terminalCommand.value;

        this.showLoading(true);
        request({ method: 'POST', url: '/api/terminal', body: JSON.stringify(req), json:true }, (err, res, body) => {
            this.showLoading(false);
            if(!this.checkResponse(err, res, body)) return;

            this.showToast(`Success!  Terminal '${req.name}' created.  Refreshing...`);

            (this as any).$.terminalName.value = undefined;
            (this as any).$.terminalImage.value = undefined;
            (this as any).$.terminalPort.value = undefined;
            (this as any).$.terminalPassword.value = undefined;
            (this as any).$.terminalShell.value = undefined;
            (this as any).$.terminalCommand.value = undefined;

            (this as any).$.terminalName.invalid = false;
            (this as any).$.terminalImage.invalid = false;

            this.setup();
        });
    }

    private deleteTerminal(e) {
        var terminalName = e.target.dataset['name'];

        this.showLoading(true);
        request({ method: 'DELETE', url: `/api/terminal/${terminalName}/` }, (err, res, body) => {
            this.showLoading(false);
            if(!this.checkResponse(err, res, body)) return;

            this.showToast(`Success!  Terminal '${terminalName}' deleted.  Refreshing...`);
            this.setup();
        });
    }

    private deleteTerminals() {
        this.showLoading(true);
        request({ method: 'DELETE', url: '/api/terminal' }, (err, res, body) => {
            this.showLoading(false);
            if(!this.checkResponse(err, res, body)) return;

            this.showToast(`Success!  Terminals deleted.  Refreshing...`);
            this.setup();
        });
    }

    private checkResponse(err, res, body): boolean {
        if(err) {
            this.showToast(JSON.stringify(err, null, 2));
            return false;
        }

        if(res.statusCode > 299 || res.statusCode < 200) {
            this.showToast(`${res.statusCode} ${res.statusText}: ${body}`);
            return false;
        }

        return true;
    }

    private showToast(s: string) {
        (this as any).$.toast.text = s;
        (this as any).$.toast.open();
    }

    private showLoading(show: boolean) {
        if(show)
            ((this as any).$.main as HTMLElement).classList.add('blur');
        else
            ((this as any).$.main as HTMLElement).classList.remove('blur');
            
        (this as any).$.loading.active = show;
    }
}