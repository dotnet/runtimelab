import { Worker, parentPort, isMainThread } from 'node:worker_threads';
import { readFile, writeFile } from 'node:fs/promises';
import { transpile } from '@bytecodealliance/jco';
import { _setPreopens } from '@bytecodealliance/preview2-shim/filesystem';
import * as http from 'node:http';

// Note that `jco` implements `wasi:io` by synchronously dispatching to worker
// threads.  That means that when we run `HttpClient` smoke test below, it will
// block the main thread waiting for I/O.  Therefore, the HTTP server we launch
// for that test needs to run in a dedicated thread rather than the main thread.

if (isMainThread) {
    // Run the tests
    const base = import.meta.url;
    const component = await readFile(new URL("./SharedLibrary.wasm", base));
    const transpiled = await transpile(component, {
        name: "shared-library",
        typescript: false,
    });
    for (const key of Object.keys(transpiled.files)) {
        await writeFile(new URL(key, base), transpiled.files[key]);
    }
    await writeFile(new URL("./shared-library.mjs", base), transpiled.files["shared-library.js"]);
    _setPreopens([]);
    const instance = await import(new URL("./shared-library.mjs", base));

    // Spawn a worker thread to run the HTTP server and feed the port number
    // it's listening on back to us when ready:
    const port = await new Promise((resolve, reject) => {
        const worker = new Worker(new URL("./SharedLibrary.mjs", base));
        worker.on("message", resolve);
        worker.on("error", reject);
        worker.on("exit", (code) => {
            reject(new Error(`worker stopped with exit code ${code}`));
        });
    });

    instance.testHttp(port);

    if (instance.returnsPrimitiveInt() != 10)
        process.exit(1);

    if (instance.returnsPrimitiveBool() != 1)
        process.exit(2);

    if (instance.returnsPrimitiveChar() != 'a')
        process.exit(3);

    // As long as no unmanaged exception is thrown managed class loaders were initialized successfully.
    instance.ensureManagedClassLoaders();

    if (instance.checkSimpleGcCollect() != 100)
        process.exit(4);

    if (instance.checkSimpleExceptionHandling() != 100)
        process.exit(5);

    process.exit(100);
} else {
    // Run the HTTP server
    const server = http.createServer((req, res) => {
        if (req.method === "POST" && req.url === "/echo") {
            // Note that we buffer the request body here rather than pipe it
            // directly to the response.  That's because, as of this writing,
            // `WasiHttpHandler` sends the entire request body before reading
            // any of the response body, which can lead to deadlock if the
            // server is blocked on backpressure when sending the response body.
            let chunks = [];
            req.on("data", (chunk) => {
                chunks.push(chunk);
            });
            req.on("end", () => {
                res.writeHead(200, req.headers);
                res.end(Buffer.concat(chunks));
            });
        } else if (req.method === "GET" && req.url === "/slow-hello") {
            setTimeout(() => {
                const body = "hola";
                res
                    .writeHead(200, {
                        "content-length": Buffer.byteLength(body),
                        "content-type": "text/plain",
                    })
                    .end(body);
            }, 10 * 1000);
        } else {
            let status;
            let body;
            if (req.method === "GET" && req.url === "/hello") {
                status = 200;
                body = "hola";
            } else {
                status = 400;
                body = "Bad Request";
            }
            res
                .writeHead(status, {
                    "content-length": Buffer.byteLength(body),
                    "content-type": "text/plain",
                })
                .end(body);
        }
    });
    server.listen(() => {
        parentPort.postMessage(server.address().port);
    });
}
