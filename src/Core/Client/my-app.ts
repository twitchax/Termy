import '@polymer/paper-card/paper-card';
import '@polymer/iron-list/iron-list';
import '@polymer/paper-item/paper-item';
import '@polymer/paper-button/paper-button';
import '@polymer/paper-toast/paper-toast';
import '@polymer/paper-input/paper-input';
import '@polymer/paper-input/paper-textarea';
import '@polymer/paper-spinner/paper-spinner';
import '@granite-elements/granite-c3';

import { PolymerElement, html } from '@polymer/polymer/polymer-element';
import { customElement, property, query } from "@polymer/decorators";
import { PaperInputElement } from '@polymer/paper-input/paper-input';
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
    //@query('#terminalPort')
    //terminalPort!: PaperInputElement;
    @query('#terminalCnames')
    terminalCnames!: PaperInputElement;
    @query('#terminalPassword')
    terminalPassword!: PaperInputElement;
    @query('#terminalShell')
    terminalShell!: PaperInputElement;
    @query('#terminalCommand')
    terminalCommand!: PaperTextareaElement;
    @query('#toast')
    toast!: PaperToastElement;
    @query('#su')
    su!: PaperInputElement;

    nodeCpuPercentData: any;
    nodeMemoryPercentData: any;
    chartAxis: any;

    firstRun: boolean = true;

    ready() {
        super.ready();

        this.setup();

        // Start a timer to constantly get node statistics.
        this.updateNodeStats();
        setInterval(this.updateNodeStats.bind(this), 10000);
    }

    private setup() {
        // Get the list of running terminals.
        this.showLoading(true);
        request<Bll.Terminal[]>({ url: '/api/terminal' }).then(data => {
            this.terminals = data;
        }).catch(this.handleError.bind(this)).finally(this.showLoading.bind(this, false));
    }

    private updateNodeStats() {
        request<{ [name: string]: Bll.NodeStat[]; }>({ url: '/api/node/stats' }).then(nodes => {
            let cpuPercentColumns: (string | number)[][] = [];
            let memoryPercentColumns: (string | number)[][] = [];
            let xs = nodes[Object.keys(nodes)[0]].map(n => n.time);

            //columns.push(['x', xs].flat());

            for(var key in nodes) {
                cpuPercentColumns.push([key, nodes[key].map(s => s.cpuPercent)].flat());
                memoryPercentColumns.push([key, nodes[key].map(s => s.memoryPercent)].flat());
            }

            this.nodeCpuPercentData = {
                columns: cpuPercentColumns
            };
            this.nodeMemoryPercentData = {
                columns: memoryPercentColumns
            };
        });
    }

    private createTerminal() {
        let req = this.createTerminalRequestObject;
        
        this.showLoading(true);
        this.suRequest({ url: '/api/terminal', method: 'POST', body: req }).then(() => {
            this.showToast(`Success!  Terminal '${req.name}' created.  Refreshing...`);
            this.clearCreateTerminalValues();
        }).catch(this.handleError.bind(this)).finally(this.setup.bind(this));
    }

    private deleteTerminal(e: any | MouseEvent) {
        var terminalName = e.target.dataset['name'];

        this.showLoading(true);
        this.suRequest({ url: `/api/terminal/${terminalName}/`, method: 'DELETE' })
            .then(this.showToast.bind(this, `Success!  Terminal '${terminalName}' deleted.  Refreshing...`))
            .catch(this.handleError.bind(this))
            .finally(this.setup.bind(this));
    }

    private deleteTerminals() {
        this.showLoading(true);
        this.suRequest({ url: '/api/terminal', method: 'DELETE' })
            .then(this.showToast.bind(this, `Success!  Terminals deleted.  Refreshing...`))
            .catch(this.handleError.bind(this))
            .finally(this.setup.bind(this));
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
        //req.port = resolveNumber(this.terminalPort.value);
        req.password = resolveString(this.terminalPassword.value);
        req.shell = resolveString(this.terminalShell.value);
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
                    <!-- Remove this for now, until k8s ingresses support port mappings. -->
                    <!-- <paper-input id="terminalPort" label="Port"></paper-input> -->
                    <paper-input id="terminalPassword" label="Password" type="password"></paper-input>
                    <paper-input id="terminalShell" label="Shell"></paper-input>
                    <paper-textarea id="terminalCommand" label="Start Command"></paper-textarea>

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
                                    <th>CNAMEs</th>
                                    <th>Delete?</th>
                                </tr>
                            </thead>
                            <tbody>
                                <template is="dom-repeat" items="[[terminals]]" as="terminal">
                                    <tr>
                                        <td>[[terminal.name]]</td>
                                        <td>
                                            <ul style="list-style: none;">
                                                <dom-repeat items="[[terminal.cnameMaps]]">
                                                    <template>
                                                        <li><a href="https://[[item.name]]/">https://[[item.name]]/</a> => [[item.port]]</li>
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

                    <paper-card>
                        <h1>Node Statistics (2 hours)</h1>

                        <h3>CPU Percentage</h3>
                        <granite-c3 data="[[nodeCpuPercentData]]" point='{ "show": false }' axis='{ "y": { "min": 0, "max": 100 } }'></granite-c3>

                        <h3>Memory Percentage</h3>
                        <granite-c3 data="[[nodeMemoryPercentData]]" point='{ "show": false }' axis='{ "y": { "min": 0, "max": 100 } }'></granite-c3>
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