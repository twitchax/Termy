import { PolymerElement } from '@polymer/polymer/polymer-element';
import * as request from 'browser-request';

import '@polymer/paper-card/paper-card';
import '@polymer/iron-list/iron-list';
import '@polymer/paper-item/paper-item';

import * as view from './app.template.html';

export class MyApp extends PolymerElement {
    static get properties() {
        return {
            terminals: Array
        };
    }

    terminals!: Bll.Terminal[];

    constructor() {
        super();
        
        this.setup();
    }

    async setup() {
        request('/api/terminals', (err, response, body) => {
            this.terminals = JSON.parse(body);
        });
        this.terminals = await request('/api/terminals');
    }

    static get template() {
        return view;
    }
}