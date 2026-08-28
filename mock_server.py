import http.server
import socketserver
import time
import json
import random

PORT = 8080

class MockProviderHandler(http.server.SimpleHTTPRequestHandler):
    def do_POST(self):
        if self.path.endswith("/v1/chat/completions"):
            content_length = int(self.headers.get('Content-Length', 0))
            body = self.rfile.read(content_length)
            data = json.loads(body) if body else {}
            is_stream = data.get("stream", False)

            # Simulate upstream processing latency (e.g. 50ms to 200ms)
            time.sleep(random.uniform(0.05, 0.2))
            
            self.send_response(200)
            if is_stream:
                self.send_header('Content-Type', 'text/event-stream')
                self.end_headers()
                
                chunks = ["This", " is", " a", " streamed", " mocked", " response."]
                for chunk in chunks:
                    chunk_data = {
                        "id": "chatcmpl-mock",
                        "object": "chat.completion.chunk",
                        "created": int(time.time()),
                        "model": "gpt-4o-mini",
                        "choices": [{"index": 0, "delta": {"content": chunk}, "finish_reason": None}]
                    }
                    self.wfile.write(f"data: {json.dumps(chunk_data)}\n\n".encode())
                    time.sleep(0.05)
                
                final_data = {
                    "id": "chatcmpl-mock",
                    "object": "chat.completion.chunk",
                    "created": int(time.time()),
                    "model": "gpt-4o-mini",
                    "choices": [{"index": 0, "delta": {}, "finish_reason": "stop"}]
                }
                self.wfile.write(f"data: {json.dumps(final_data)}\n\n".encode())
                self.wfile.write(b"data: [DONE]\n\n")
            else:
                self.send_header('Content-Type', 'application/json')
                self.end_headers()
                
                response = {
                    "id": "chatcmpl-mock",
                    "object": "chat.completion",
                    "created": int(time.time()),
                    "model": "gpt-4o-mini",
                    "choices": [{
                        "index": 0,
                        "message": {
                            "role": "assistant",
                            "content": "This is a mocked response."
                        },
                        "finish_reason": "stop"
                    }],
                    "usage": {
                        "prompt_tokens": 10,
                        "completion_tokens": 5,
                        "total_tokens": 15
                    }
                }
                self.wfile.write(json.dumps(response).encode())
        else:
            self.send_response(404)
            self.end_headers()

with socketserver.ThreadingTCPServer(("", PORT), MockProviderHandler) as httpd:
    print(f"Serving mock provider at port {PORT}")
    httpd.serve_forever()
