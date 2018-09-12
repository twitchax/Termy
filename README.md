# Termy

A docker image that lives in a Kube cluster which takes requests and spins up web terminals on the cluster.

## Information

### Install

To build the primary pod, just build the docker image.

```bash
docker build -t twitchax/termy .
```

The Kube cluster must define a few secrets called `termysecrets` (look at `./tools/createSecrets.sh`):
* `supw`: a file that defines a password for the super user (allows killing the pod, etc.).
* `azlogin`: a file with an Azure service principal with access to a DNS host which takes the form `login --service-principal -u <guid> -p <passphrase> --tenant <guid>`.
* `cert.pfx`: the SSL cert.
* `certpw`: the SSL cert passphrase.
* `kubeconfig`: the k8s config for the cluster itself (inception, yes).

Finally, the primary pod must be created and exposed.

```bash
./tools/create.sh
```

Optionally, use a registrar to expose your primary pod friendily.

### Test

### Examples

Navigate to the cluster endpoint to see some rudimentary UI, or make CURL requests.

```bash
curl -X POST \
    -H "Content-Type: application/json" \
    -d '{ "Name": "myubuntu", "Image": "ubuntu" }' \
    http://mytermydeployment.com/api/terminal
```

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