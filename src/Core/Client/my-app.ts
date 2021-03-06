import '@polymer/paper-card/paper-card';
import '@polymer/iron-list/iron-list';
import '@polymer/paper-item/paper-item';
import '@polymer/paper-button/paper-button';
import '@polymer/paper-toast/paper-toast';
import '@polymer/paper-input/paper-input';
import '@polymer/paper-radio-group/paper-radio-group';
import '@polymer/paper-radio-button/paper-radio-button';
import '@polymer/paper-input/paper-textarea';
import '@polymer/paper-spinner/paper-spinner';
import '@granite-elements/granite-c3';

import { PolymerElement, html } from '@polymer/polymer/polymer-element';
import { customElement, property, query } from "@polymer/decorators";
import { PaperInputElement } from '@polymer/paper-input/paper-input';
import { PaperRadioGroupElement } from '@polymer/paper-radio-group/paper-radio-group';
import { PaperTextareaElement } from '@polymer/paper-input/paper-textarea';
import { PaperToastElement } from '@polymer/paper-toast/paper-toast';
import { PaperSpinnerElement } from '@polymer/paper-spinner/paper-spinner';

import { request, Request } from './helpers';

@customElement('my-app')
export class MyApp extends PolymerElement {
    
    @property({ type: Array })
    terminals: Bll.Terminal[] = [];

    @query('#terminalName')
    terminalName!: PaperInputElement;
    @query('#main')
    main!: HTMLDivElement;
    @query('#loading')
    loading!: PaperSpinnerElement;
    @query('#terminalImage')
    terminalImage!: PaperInputElement;
    @query('#terminalCnames')
    terminalCnames!: PaperInputElement;
    @query('#terminalPassword')
    terminalPassword!: PaperInputElement;
    @query('#terminalShell')
    terminalShell!: PaperInputElement;
    @query('#terminalEntrypoint')
    terminalEntrypoint!: PaperRadioGroupElement;
    @query('#terminalEnvironmentVariables')
    terminalEnvironmentVariables!: PaperTextareaElement;
    @query('#terminalCommand')
    terminalCommand!: PaperTextareaElement;
    @query('#toast')
    toast!: PaperToastElement;
    @query('#su')
    su!: PaperInputElement;

    nodeCpuPercentData: any;
    nodeMemoryPercentData: any;
    chartAxis: any;
    terminalEntrypointSelectedValue = "default";

    ready() {
        super.ready();

        // Always make update requests on visibility change.
        document.addEventListener('visibilitychange', () => {
            this.update();
        });

        // Start a timer to constantly get terminals and node stats.
        this.update();
        setInterval(this.update.bind(this), 10000);
    }

    private async update() {
        if(document.hidden)
            return;

        await this.updateTerminalList();

        this.showLoading(false);
    }

    private updateTerminalList() {
        return request<Bll.Terminal[]>({ url: '/api/terminal' }).then(data => {
            this.terminals = data;
        }).catch(this.handleError.bind(this));
    }

    private createTerminal() {
        let req = this.createTerminalRequestObject;
        
        this.showLoading(true);
        this.suRequest({ url: '/api/terminal', method: 'POST', body: req }).then(() => {
            this.showToast(`Success!  Terminal '${req.name}' created.  Refreshing...`);
            this.clearCreateTerminalValues();
        }).catch(this.handleError.bind(this)).finally(this.update.bind(this));
    }

    private deleteTerminal(e: any | MouseEvent) {
        var terminalName = e.target.dataset['name'];

        this.showLoading(true);
        this.suRequest({ url: `/api/terminal/${terminalName}/`, method: 'DELETE' })
            .then(this.showToast.bind(this, `Success!  Terminal '${terminalName}' deleted.  Refreshing...`))
            .catch(this.handleError.bind(this))
            .finally(this.update.bind(this));
    }

    private deleteTerminals() {
        this.showLoading(true);
        this.suRequest({ url: '/api/terminal', method: 'DELETE' })
            .then(this.showToast.bind(this, `Success!  Terminals deleted.  Refreshing...`))
            .catch(this.handleError.bind(this))
            .finally(this.update.bind(this));
    }

    private handleError(err: any): void {
        if(err) {
            this.showToast(err);
        }
    }

    private suRequest<T>(obj: Request): Promise<T> {
        if(obj.headers === undefined)
            obj.headers = {};

        obj.headers["X-Super-User-Password"] = this.su.value;

        return request(obj);
    }

    private get createTerminalRequestObject() {
        var req = {} as Bll.CreateTerminalRequest;

        req.name = resolveString(this.terminalName.value) || '';
        req.image = resolveString(this.terminalImage.value) || '';
        req.cnames = resolveString(this.terminalCnames.value);
        req.password = resolveString(this.terminalPassword.value);
        req.shell = resolveString(this.terminalShell.value);
        req.entrypoint = resolveString(this.terminalEntrypoint.selected as string);
        req.environmentVariables = resolveString(this.terminalEnvironmentVariables.value);
        req.command = resolveString(this.terminalCommand.value);

        return req;

        function resolveString(text?: string | null) {
            if(text === '' || text === undefined || text === null)
                return undefined;

            return text;
        }

        function resolveNumber(text?: string | null) {
            if(text === '' || text === undefined || text === null)
                return undefined;
            
            return parseInt(text);
        }
    }

    private clearCreateTerminalValues() {
        this.terminalName.value = undefined;
        this.terminalImage.value = undefined;
        this.terminalCnames.value = undefined;
        this.terminalPassword.value = undefined;
        this.terminalShell.value = undefined;
        this.terminalEntrypoint.selected = "default";
        this.terminalEnvironmentVariables.value = undefined;
        this.terminalCommand.value = undefined;

        this.terminalName.invalid = false;
        this.terminalImage.invalid = false;
    }

    private showToast(s: string) {
        this.toast.text = s;
        this.toast.open();
    }

    private showLoading(show: boolean) {
        if(show)
            this.main.classList.add('blur');
        else
            this.main.classList.remove('blur');
            
        this.loading.active = show;
    }

    private isEntrypointCustom(terminalEntrypointSelectedValue: string) {
        return terminalEntrypointSelectedValue === 'custom';
    }

    static get template() {
        return html`
            <paper-toast duration="10000" id="toast"></paper-toast>

            <paper-spinner id="loading" style="position: absolute; left: 50vw; top: 50vh;"></paper-spinner>

            <paper-input id="su" type="password" label="Super User Password" style="position: fixed; top: 10px; right: 10px;"></paper-input>

            <div id="main" style="display: flex; flex-direction: row; align-items: flex-start; flex-wrap: wrap;">
                <!-- Left pane -->
                <paper-card style="max-width: 400px;">
                    <h1>Create Terminal</h1>

                    <paper-input id="terminalName" label="Name" required auto-validate error-message="Required."></paper-input>
                    <paper-input id="terminalImage" label="Image" required auto-validate error-message="Required."></paper-input>
                    <paper-input id="terminalCnames" label="CNAMEs"></paper-input>
                    <paper-input id="terminalPassword" label="Password" type="password"></paper-input>
                    <paper-input id="terminalShell" label="Shell"></paper-input>

                    <br />Entrypoint<br />
                    <paper-radio-group id="terminalEntrypoint" selected="{{terminalEntrypointSelectedValue}}">
                        <paper-radio-button name="default">Default</paper-radio-button>
                        <paper-radio-button name="container">Container</paper-radio-button>
                        <paper-radio-button name="custom">Custom Script</paper-radio-button>
                    </paper-radio-group>
                    
                    <paper-textarea id="terminalCommand" label="Start Command" hidden$="[[!isEntrypointCustom(terminalEntrypointSelectedValue)]]"></paper-textarea>
                    <paper-textarea id="terminalEnvironmentVariables" label="Environment Variables"></paper-textarea>
                    

                    <br />
                    <br />

                    <paper-button on-tap="createTerminal" raised>Create</paper-button>
                </paper-card>

                <!-- Center pane. -->
                <div style="display: flex; flex-direction: column; overflow: auto; height: 95vh; min-width: 800px;">
                    <paper-card>
                        <h1>Terminals</h1>

                        <br />

                        <table>
                            <thead>
                                <tr>
                                    <th>Name</th>
                                    <th>Replicas</th>
                                    <th>CNAMEs</th>
                                    <th>Delete?</th>
                                </tr>
                            </thead>
                            <tbody>
                                <template is="dom-repeat" items="[[terminals]]" as="terminal">
                                    <tr>
                                        <td>[[terminal.name]]</td>
                                        <td>[[terminal.replicas]]</td>
                                        <td>
                                            <ul style="list-style: none;">
                                                <dom-repeat items="[[terminal.cnameMaps]]">
                                                    <template>
                                                        <li><a href="http://[[item.name]]/">http://[[item.name]]/</a> => [[item.port]]</li>
                                                    </template>
                                                </dom-repeat>
                                            </ul>
                                        </td>
                                        <td>
                                            <paper-button data-name$="[[terminal.name]]" on-tap="deleteTerminal" raised>Delete</paper-button>
                                        </td>
                                    </tr>
                                </template>
                            </tbody>
                        </table>

                        <br />
                        <br />

                        <paper-button on-tap="deleteTerminals" raised>Delete All</paper-button>
                    </paper-card>
                </div>
            </div>

            <style>
                paper-card {
                    padding: 20px;
                    margin: 10px;
                }

                granite-c3 {
                    width: 100%;
                }

                .blur {
                    filter: blur(5px);
                }

                table {
                    border: 2px solid #FFFFFF;
                    width: 100%;
                    text-align: center;
                    border-collapse: collapse;
                }

                table td,
                table th {
                    border: 1px solid #FFFFFF;
                    padding: 3px 4px;
                }

                table tbody td {
                    font-size: 13px;
                }

                table thead {
                    background: #FFFFFF;
                    border-bottom: 4px solid #333333;
                }

                table thead th {
                    font-size: 15px;
                    font-weight: bold;
                    color: #333333;
                    text-align: center;
                    border-left: 2px solid #333333;
                }

                table thead th:first-child {
                    border-left: none;
                }

                table tfoot {
                    font-size: 14px;
                    font-weight: bold;
                    color: #333333;
                    border-top: 4px solid #333333;
                }

                table tfoot td {
                    font-size: 14px;
                }
            </style>
        `;
    }
}