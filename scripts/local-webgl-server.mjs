import fs from "node:fs";
import http from "node:http";
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

const server = http.createServer((request, response) => {
  let filePath = resolveRequestPath(request.url);

  if (!filePath || !fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    filePath = path.join(root, "index.html");
  }

  const headers = {
    "Access-Control-Allow-Origin": "*",
    "Cache-Control": "no-store",
    "Cross-Origin-Opener-Policy": "same-origin",
    "Cross-Origin-Embedder-Policy": "require-corp",
  };

  let extension = path.extname(filePath);
  if (extension === ".gz") {
    headers["Content-Encoding"] = "gzip";
    extension = path.extname(filePath.slice(0, -3));
  }

  headers["Content-Type"] = mimeTypes[extension] || "application/octet-stream";
  response.writeHead(200, headers);

  if (request.method === "HEAD") {
    response.end();
    return;
  }

  fs.createReadStream(filePath).pipe(response);
});

server.listen(port, "0.0.0.0", () => {
  console.log(`Appreciators WebGL local server listening on http://0.0.0.0:${port}`);
  console.log(`Serving ${root}`);
});
