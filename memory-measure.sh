#!/bin/bash
# Memory measurement script for MiniRouter
# Usage: ./memory-measure.sh <pid> or ./memory-measure.sh (auto-detects)

PID="$1"
SAMPLE_INTERVAL="${2:-2}"
DURATION_SEC="${3:-60}"

if [ -z "$PID" ]; then
    PID=$(pgrep -f MininRouter)
fi

if [ -z "$PID" ]; then
    echo "No MininRouter process found. Start the server first." >&2
    exit 1
fi

echo "=== Memory Measurement ==="
echo "PID: $PID"
echo "Sample interval: ${SAMPLE_INTERVAL}s"
echo "Duration: ${DURATION_SEC}s"
echo ""

MIN_RSS=999999999
MAX_RSS=0

for ((i=0; i<DURATION_SEC/SAMPLE_INTERVAL; i++)); do
    RSS_KB=$(ps -o rss= -p $PID 2>/dev/null || echo 0)
    if [ -n "$RSS_KB" ] && [ "$RSS_KB" != "0" ]; then
        RSS_MB=$(awk "BEGIN {printf \"%.2f\", $RSS_KB/1024}")
        echo "[$(date +%T)] RSS: ${RSS_MB} MB ($RSS_KB KB)"
        
        if [ "$RSS_KB" -lt "$MIN_RSS" ]; then
            MIN_RSS=$RSS_KB
        fi
        if [ "$RSS_KB" -gt "$MAX_RSS" ]; then
            MAX_RSS=$RSS_KB
        fi
    else
        echo "[$(date +%T)] Process not responding"
    fi
    
    sleep $SAMPLE_INTERVAL
done

echo ""
echo "=== Summary ==="
echo "Minimum RSS: $(awk "BEGIN {printf \"%.2f\", $MIN_RSS/1024}") MB"
echo "Maximum RSS: $(awk "BEGIN {printf \"%.2f\", $MAX_RSS/1024}") MB"
