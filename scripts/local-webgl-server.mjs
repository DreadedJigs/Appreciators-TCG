import fs from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import process from "node:process";

process.title = "Appreciators WebGL local server";

const root = path.resolve(process.argv[2] || "unity-client/Builds");
const port = Number(process.argv[3] || 8088);

const mimeTypes = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".wasm": "application/wasm",
  ".data": "application/octet-stream",
  ".unityweb": "application/octet-stream",
  ".json": "application/json; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".png": "image/png",
  ".ico": "image/x-icon",
};

function resolveRequestPath(url = "/") {
  const requestPath = decodeURIComponent(url.split("?")[0]);
  const normalized = path.normalize(requestPath === "/" ? "/index.html" : requestPath).replace(/^[/\\]+/, "");
  const filePath = path.join(root, normalized);
  return filePath.startsWith(root) ? filePath : null;
}

function sharedHeaders(contentType = "application/json; charset=utf-8") {
  return {
    "Access-Control-Allow-Origin": "*",
    "Cache-Control": "no-store",
    "Cross-Origin-Opener-Policy": "same-origin",
    "Cross-Origin-Embedder-Policy": "require-corp",
    "Content-Type": contentType,
  };
}

function findLanAddress() {
  const candidates = [];
  for (const addresses of Object.values(os.networkInterfaces())) {
    for (const address of addresses || []) {
      if (address.family !== "IPv4" || address.internal || address.address.startsWith("169.254.")) {
        continue;
      }

      candidates.push(address.address);
    }
  }

  return (
    candidates.find((address) => address.startsWith("192.168.")) ||
    candidates.find((address) => address.startsWith("10.")) ||
    candidates.find((address) => /^172\.(1[6-9]|2\d|3[0-1])\./.test(address)) ||
    candidates[0] ||
    "127.0.0.1"
  );
}

function mobileUrl() {
  return `http://${findLanAddress()}:${port}/?mobile=1`;
}

function injectMobileUrl(html) {
  const payload = `<script>window.APPRECIATORS_MOBILE_URL=${JSON.stringify(mobileUrl())};</script>`;
  if (html.includes("window.APPRECIATORS_MOBILE_URL")) {
    return html;
  }

  return html.replace("<head>", `<head>\n    ${payload}`);
}

const server = http.createServer((request, response) => {
  const requestPath = decodeURIComponent((request.url || "/").split("?")[0]);

  if (requestPath === "/__appreciators/mobile-url") {
    if (request.method === "OPTIONS") {
      response.writeHead(204, {
        ...sharedHeaders(),
        "Access-Control-Allow-Methods": "GET, OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type",
      });
      response.end();
      return;
    }

    const body = JSON.stringify({
      mobileUrl: mobileUrl(),
      note: "Scan this from a phone on the same Wi-Fi/network as this PC.",
    });
    response.writeHead(200, {
      ...sharedHeaders(),
      "Content-Length": Buffer.byteLength(body),
    });
    response.end(body);
    return;
  }

  let filePath = resolveRequestPath(request.url);

  if (!filePath || !fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    filePath = path.join(root, "index.html");
  }

  const headers = sharedHeaders();

  const stat = fs.statSync(filePath);
  let extension = path.extname(filePath);
  if (extension === ".gz") {
    headers["Content-Encoding"] = "gzip";
    extension = path.extname(filePath.slice(0, -3));
  }

  headers["Content-Type"] = mimeTypes[extension] || "application/octet-stream";

  if (extension === ".html" && path.basename(filePath).toLowerCase() === "index.html") {
    const body = injectMobileUrl(fs.readFileSync(filePath, "utf8"));
    headers["Content-Length"] = Buffer.byteLength(body);
    headers["Last-Modified"] = stat.mtime.toUTCString();
    response.writeHead(200, headers);
    if (request.method === "HEAD") {
      response.end();
      return;
    }

    response.end(body);
    return;
  }

  headers["Content-Length"] = stat.size;
  headers["Last-Modified"] = stat.mtime.toUTCString();
  headers["Accept-Ranges"] = "bytes";
  response.writeHead(200, headers);

  if (request.method === "HEAD") {
    response.end();
    return;
  }

  fs.createReadStream(filePath).pipe(response);
});

server.listen(port, "0.0.0.0", () => {
  console.log(`Appreciators WebGL local server listening on http://0.0.0.0:${port}`);
  console.log(`Mobile QR URL: ${mobileUrl()}`);
  console.log(`Serving ${root}`);
});
