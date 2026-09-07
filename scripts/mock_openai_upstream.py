#!/usr/bin/env python3
"""Minimal OpenAI-compatible mock upstream for scripts/aot-smoke.sh (issue #10).

Serves POST /v1/chat/completions on 127.0.0.1:18099 with a fixed non-streaming
Chat Completions response, so the AOT smoke test can assert the full
Anthropic -> OpenAI -> Anthropic translation round trip through /v1/messages.
"""
import json
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

MOCK_RESPONSE = {
    "id": "chatcmpl-mock",
    "object": "chat.completion",
    "created": 1,
    "model": "mock-model",
    "choices": [
        {
            "index": 0,
            "message": {"role": "assistant", "content": "Hello from mock"},
            "finish_reason": "stop",
        }
    ],
    "usage": {"prompt_tokens": 5, "completion_tokens": 3, "total_tokens": 8},
}


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def _read_body(self):
        te = (self.headers.get("Transfer-Encoding") or "").lower()
        if "chunked" in te:
            body = b""
            while True:
                size_line = self.rfile.readline().strip()
                size = int(size_line.split(b";")[0] or b"0", 16)
                if size == 0:
                    self.rfile.readline()  # trailing CRLF
                    break
                body += self.rfile.read(size)
                self.rfile.readline()  # CRLF after chunk
            return body
        length = int(self.headers.get("Content-Length", 0))
        return self.rfile.read(length) if length else b""

    def _validate_request(self, body):
        """Assert the router translated the Anthropic request into Chat
        Completions shape (issue #18): with this, the smoke covers the
        request side of the translation, not just the response side.
        Returns an error string, or None when the request looks right."""
        try:
            req = json.loads(body or b"{}")
        except ValueError:
            return "request body is not valid JSON"
        if req.get("model") != "mock-model":
            return f"expected model 'mock-model', got {req.get('model')!r}"
        messages = req.get("messages")
        if not isinstance(messages, list) or not messages:
            return f"expected a non-empty 'messages' array, got {messages!r}"
        for m in messages:
            if not isinstance(m, dict) or m.get("role") not in ("system", "user", "assistant") or "content" not in m:
                return f"message missing role/content or unknown role: {m!r}"
        return None

    def do_POST(self):
        body = self._read_body()  # drain request body before replying
        if self.path != "/v1/chat/completions":
            self.send_error(404)
            return
        error = self._validate_request(body)
        if error:
            print(f"mock_openai_upstream: rejecting request: {error}", file=sys.stderr)
            resp = json.dumps({"error": {"message": f"request-side validation failed: {error}"}}).encode()
            self.send_response(400)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(resp)))
            self.end_headers()
            self.wfile.write(resp)
            return
        resp = json.dumps(MOCK_RESPONSE).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(resp)))
        self.end_headers()
        self.wfile.write(resp)

    def log_message(self, fmt, *args):
        pass


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 18099
    HTTPServer(("127.0.0.1", port), Handler).serve_forever()
