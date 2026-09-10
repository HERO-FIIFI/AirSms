#!/bin/sh
set -eu

cat > /usr/share/nginx/html/env.js <<EOF
window.__AIRSMS_CONFIG__ = {
  apiBaseUrl: "${VITE_API_BASE_URL:-http://localhost:5000}",
  notificationsApiBaseUrl: "${VITE_NOTIFICATIONS_API_BASE_URL:-http://localhost:5001}"
};
EOF
