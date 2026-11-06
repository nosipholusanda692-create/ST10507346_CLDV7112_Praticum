// AgriStream IoT - load test (k6). Works locally AND in Azure Load Testing (k6 test type).
//
// Simulates the peak-daylight surge: many sensors POSTing telemetry batches to the HTTP function
// (which queues them), plus dashboards reading data and hitting the web app.
//
// Environment variables (set in Azure Load Testing "Parameters" or with -e on the k6 command line):
//   WEB_URL       https://<webapp>.azurewebsites.net
//   FUNCTION_URL  https://<functionapp>.azurewebsites.net/api
//   FUNCTION_KEY  <function key>
//
// Local run:
//   k6 run -e WEB_URL=https://x.azurewebsites.net -e FUNCTION_URL=https://y.azurewebsites.net/api -e FUNCTION_KEY=abc loadtest.js

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: {
    sensor_ingest: {                       // sensors pushing data - the heavy part
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 400,
      stages: [
        { target: 50,  duration: '1m' },   // warm-up
        { target: 200, duration: '3m' },   // peak daylight surge (200 requests/s x 20 readings)
        { target: 50,  duration: '1m' },   // cool down
      ],
      exec: 'ingest',
    },
    dashboard_users: {                     // people browsing the web app / reading data
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { target: 20, duration: '2m' },
        { target: 50, duration: '2m' },
        { target: 0,  duration: '1m' },
      ],
      exec: 'browse',
    },
  },
  thresholds: {
    http_req_failed:   ['rate<0.01'],      // fewer than 1% errors
    http_req_duration: ['p(95)<1000'],     // 95% of requests under 1 s
  },
};

const WEB = __ENV.WEB_URL;
const FN = __ENV.FUNCTION_URL;
const KEY = __ENV.FUNCTION_KEY;

function reading(i) {
  return {
    farmId: 'farm-load',
    sensorId: `sensor-${String(Math.floor(Math.random() * 500)).padStart(3, '0')}`,
    eventType: 'reading',
    soilMoisture: Math.round((Math.random() * 60 + 5) * 10) / 10,
    soilTemperature: Math.round((Math.random() * 15 + 12) * 10) / 10,
    ph: Math.round((Math.random() * 3 + 5) * 10) / 10,
  };
}

// POST a batch of 20 readings to the HTTP-triggered function
export function ingest() {
  const batch = Array.from({ length: 20 }, (_, i) => reading(i));
  const res = http.post(`${FN}/telemetry`, JSON.stringify(batch), {
    headers: { 'Content-Type': 'application/json', 'x-functions-key': KEY },
    tags: { name: 'POST /telemetry' },
  });
  check(res, { 'ingest accepted (202)': (r) => r.status === 202 });
}

// GET latest readings from the HTTP-triggered function + hit the web app health endpoint
export function browse() {
  const r1 = http.get(`${FN}/telemetry/farm-load?limit=50`, {
    headers: { 'x-functions-key': KEY },
    tags: { name: 'GET /telemetry/{farm}' },
  });
  check(r1, { 'read ok (200)': (r) => r.status === 200 });

  const r2 = http.get(`${WEB}/health`, { tags: { name: 'GET /health' } });
  check(r2, { 'web healthy': (r) => r.status === 200 });

  const r3 = http.get(`${WEB}/Account/Login`, { tags: { name: 'GET /Account/Login' } });
  check(r3, { 'login page ok': (r) => r.status === 200 });

  sleep(1);
}
