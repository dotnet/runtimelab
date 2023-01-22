// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

const DotNetEntropyLib = {
    $DOTNETENTROPY: {
        // batchedQuotaMax is the max number of bytes as specified by the api spec.
        // If the byteLength of array is greater than 65536, throw a QuotaExceededError and terminate the algorithm.
        // https://www.w3.org/TR/WebCryptoAPI/#Crypto-method-getRandomValues
        batchedQuotaMax: 65536,
        getBatchedRandomValues: function (buffer, bufferLength) {
            // for modern web browsers
            // map the work array to the memory buffer passed with the length
            for (let i = 0; i < bufferLength; i += this.batchedQuotaMax) {
                const view = new Uint8Array(Module.HEAPU8.buffer, buffer + i, Math.min(bufferLength - i, this.batchedQuotaMax));
                crypto.getRandomValues(view)
            }
        }
    },
    dotnet_browser_entropy: function (buffer, bufferLength) {
        // check that we have crypto available
        let cryptoAvailable = typeof crypto === 'object' && typeof crypto['getRandomValues'] === 'function';

        // TODO-LLVM: Can we upstream this? Mono has this code in "polyfills.ts", part of the runtime startup.
        if (ENVIRONMENT_IS_NODE && !cryptoAvailable)
        {
            if (!globalThis.crypto) {
                globalThis.crypto = {};
            }
            if (!globalThis.crypto.getRandomValues) {
                let nodeCrypto = undefined;
                try {
                    nodeCrypto = require("node:crypto");
                } catch {
                    // Noop, error throwing polyfill provided bellow
                }

                if (!nodeCrypto) {
                    globalThis.crypto.getRandomValues = function () {
                        throw new Error("Using node without crypto support. To enable current operation, either provide polyfill for 'globalThis.crypto.getRandomValues' or enable 'node:crypto' module.");
                    };
                } else if (nodeCrypto.webcrypto) {
                    globalThis.crypto = nodeCrypto.webcrypto;
                } else if (nodeCrypto.randomBytes) {
                    globalThis.crypto.getRandomValues = function (buffer) {
                        if (buffer) {
                            buffer.set(nodeCrypto.randomBytes(buffer.length));
                        }
                    };
                }

                cryptoAvailable = true;
            }
        }

        if (cryptoAvailable) {
            DOTNETENTROPY.getBatchedRandomValues(buffer, bufferLength)
            return 0;
        } else {
            // we couldn't find a proper implementation, as Math.random() is not suitable
            // instead of aborting here we will return and let managed code handle the message
            return -1;
        }
    },
};

autoAddDeps(DotNetEntropyLib, '$DOTNETENTROPY')
mergeInto(LibraryManager.library, DotNetEntropyLib)
