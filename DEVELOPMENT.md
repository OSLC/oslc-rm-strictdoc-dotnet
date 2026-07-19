# Development guide

## Regenerating demo data

From the repository root, regenerate the StrictDoc JSON database and the
matching SPDX and HTML artifacts after changing the demo `.sdoc` or `.sgra`
files:

```sh
uvx strictdoc export src/hellow-requirements/ --output-dir src/hellow-requirements/output/ --formats json,spdx,html
```

The development server reads
`src/hellow-requirements/output/json/index.json`; commit the regenerated demo
artifacts together with their source changes.

## Local server

```sh
cd src/StrictDocOslcRmServer/StrictDocOslcRm
dotnet run
```

Run tests from the solution directory:

```sh
cd src/StrictDocOslcRmServer
dotnet run --project StrictDocOslcRm.Tests/StrictDocOslcRm.Tests.csproj -- --no-ansi --progress off
```
