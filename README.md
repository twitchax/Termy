# Termy

A docker image that lives in a Kube cluster which takes requests and spins up web terminals on the cluster.

## Information

### Install

#### Get a Cert

This will be removed once the main pod container makes these requests for you. :)

Follow the instructions in `tools/createCert.sh`.

#### Create Secrets

First, the termy service needs a few secrets to deploy along with the main pod.  They go in a directory called `.hidden` in the deploy script, but they can be put anywhere.

1. Hostname (`.hidden/hostname`)
1. Super User Password (`.hidden/supw`).
1. Kubernetes Configuration (`.hidden/kubeconfig`).
1. TLS (`tls.crt` and `tls.key`).

#### Create

Edit the script for secret location, if needed.  Then, run the deploy script.

```bash
./tools/deploy.sh
```

#### Describe Ingresses

```bash
kubectl describe ingress/termy-in --namespace=termy
kubectl describe ingress/termy-terminal-in --namespace=termy-terminals
```

Use the IPs for setting up the domain A records.

#### Create Domain

Use your favorite registrar and add A records for your `@` and `*` ingresses.



The Kube cluster must define a fe

### Test

Navigate to endpoint.

## License

```
The MIT License (MIT)

Copyright (c) 2017 Aaron Roney

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```