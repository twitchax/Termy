# Termy

A docker image that lives in a Kube cluster which takes requests and spins up web terminals on the cluster.

## Information

### Install

The Kube config must be placed in `/etc/secrets/kubeconfig` via a secrets mount or by manually placing it in the image (not recommended).

### Test

### Examples

```bash
curl -X POST \
    -H "Content-Type: application/json" \
    -d '{ "Name": "myubuntu", "Image": "ubuntu", "Tag": "<username>/myubuntuweb", "RootPassword": "pw!", "DockerUsername": "<username>", "DockerPassword": "<password>", "Shell": "/bin/bash" }' \
    http://localhost:5000/api/terminals
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