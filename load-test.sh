#!/bin/bash
# Simple concurrent load test for MiniRouter
# Usage: ./load-test.sh <base_url> <num_requests> <duration_seconds>

BASE_URL="${1:-http://localhost:5000}"
NUM_REQUESTS="${2:-50}"
DURATION_SEC="${3:-60}"

echo "=== MiniRouter Load Test ==="
echo "Target: $BASE_URL"
echo "Requests: $NUM_REQUESTS"
echo "Duration: ${DURATION_SEC}s"
echo ""

START_TIME=$(date +%s)
FAILED=0
SUCCESS=0

for ((i=1; i<=NUM_REQUESTS; i++)); do
    (
        STREAM="false"
        if [ $((i % 2)) -eq 0 ]; then
            STREAM="true"
        fi
        
        RESULT=$(curl -s -w "%{http_code}" -o /dev/null --max-time 30 \
            -X POST "$BASE_URL/v1/chat/completions" \
            -H "Content-Type: application/json" \
            -H "Authorization: Bearer sk-00e3238e7a8f93e2bb067bf8c92c408ce77963c9be253b23c149d649ae2564a0" \
            -d '{"model":"gpt-4o-mini","messages":[{"role":"user","content":"Test '"$i"'"}],"stream":'"$STREAM"'}')

        
        if [ "$RESULT" = "200" ]; then
            echo "SUCCESS" >> .test_results
        else
            echo "Request $i failed with status $RESULT" >&2
            echo "FAILED" >> .test_results
        fi
    ) &
    
    # Limit concurrency to 50 parallel requests
    if (( i % 50 == 0 )); then
        wait
    fi
done

wait

SUCCESS=$(grep -c SUCCESS .test_results || true)
FAILED=$(grep -c FAILED .test_results || true)
rm -f .test_results

END_TIME=$(date +%s)
ELAPSED=$((END_TIME - START_TIME))

echo ""
echo "=== Results ==="
echo "Completed: $((SUCCESS + FAILED)) requests in ${ELAPSED}s"
echo "Successful: $SUCCESS"
echo "Failed: $FAILED"
echo "Time Elapsed: ${ELAPSED}s"

exit $((FAILED > 0 ? 1 : 0))
