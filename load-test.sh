#!/bin/bash
# Simple concurrent load test for MiniRouter
# Usage: MINIROUTER_API_KEY=ak_... ./load-test.sh <base_url> <num_requests> <duration_seconds>
#
# The model follows MiniRouter's explicit routing format: providerId/modelName
# Override with MINIROUTER_MODEL if your provider id differs.

BASE_URL="${1:-http://localhost:8080}"
NUM_REQUESTS="${2:-50}"
DURATION_SEC="${3:-60}"
API_KEY="${MINIROUTER_API_KEY:-}"
MODEL="${MINIROUTER_MODEL:-openai-primary/gpt-4o-mini}"

if [ -z "$API_KEY" ]; then
    echo "Error: MINIROUTER_API_KEY is required (create one via POST /api/keys)." >&2
    echo "Usage: MINIROUTER_API_KEY=ak_... $0 [base_url] [num_requests] [duration_seconds]" >&2
    exit 2
fi

echo "=== MiniRouter Load Test ==="
echo "Target: $BASE_URL"
echo "Model:  $MODEL (explicit routing: providerId/model)"
echo "Requests: $NUM_REQUESTS"
echo "Duration: ${DURATION_SEC}s"
echo ""

RESULTS_FILE=$(mktemp)
trap 'rm -f "$RESULTS_FILE"' EXIT

START_TIME=$(date +%s)

for ((i=1; i<=NUM_REQUESTS; i++)); do
    (
        STREAM="false"
        if [ $((i % 2)) -eq 0 ]; then
            STREAM="true"
        fi

        RESULT=$(curl -s -w "%{http_code}" -o /dev/null --max-time 30 \
            -X POST "$BASE_URL/v1/chat/completions" \
            -H "Content-Type: application/json" \
            -H "Authorization: Bearer $API_KEY" \
            -d '{"model":"'"$MODEL"'","messages":[{"role":"user","content":"Test '"$i"'"}],"stream":'"$STREAM"'}')

        if [ "$RESULT" = "200" ]; then
            echo "SUCCESS" >> "$RESULTS_FILE"
        else
            echo "Request $i failed with status $RESULT" >&2
            echo "FAILED" >> "$RESULTS_FILE"
        fi
    ) &

    # Limit concurrency to 50 parallel requests
    if (( i % 50 == 0 )); then
        wait
    fi
done

wait

SUCCESS=$(grep -c SUCCESS "$RESULTS_FILE" || true)
FAILED=$(grep -c FAILED "$RESULTS_FILE" || true)

END_TIME=$(date +%s)
ELAPSED=$((END_TIME - START_TIME))

echo ""
echo "=== Results ==="
echo "Completed: $((SUCCESS + FAILED)) requests in ${ELAPSED}s"
echo "Successful: $SUCCESS"
echo "Failed: $FAILED"
echo "Time Elapsed: ${ELAPSED}s"

exit $((FAILED > 0 ? 1 : 0))
