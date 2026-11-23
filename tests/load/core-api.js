// k6 load test of the Core API read path: login, recipe list, cost and timeline calculations.
//
//   k6 run -e BASE_URL=http://localhost:5080 tests/load/core-api.js            (registers a throw-away account)
//   k6 run -e BASE_URL=... -e EMAIL=... -e PASSWORD=... tests/load/core-api.js  (existing account)
//
// The thresholds are the performance requirement: p95 below 1 s and less than 1 % failed requests at 20 virtual users.
import http from 'k6/http';
import { check, group, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080';
const API = `${BASE_URL}/api/v1`;

export const options = {
  scenarios: {
    bakers: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '2m', target: 20 },
        { duration: '20s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
    'http_req_duration{name:cost}': ['p(95)<1000'],
    'http_req_duration{name:timeline}': ['p(95)<1000'],
  },
};

// Without EMAIL/PASSWORD a fresh account is registered and a recipe is copied from the public library into it.
// The auth endpoints are rate limited per IP, so setup logs in once and every virtual user reuses the token.
export function setup() {
  const json = { headers: { 'Content-Type': 'application/json' } };
  let email = __ENV.EMAIL;
  let password = __ENV.PASSWORD;
  if (!email || !password) {
    email = `k6-${Date.now()}@example.test`;
    password = `Load-test-${Math.random().toString(36).slice(2)}-1A`;
    const register = http.post(`${API}/auth/register`, JSON.stringify({ email, password, displayName: 'k6 baker' }), json);
    check(register, { 'register 201': (r) => r.status === 201 });
  }

  const login = http.post(`${API}/auth/login`, JSON.stringify({ email, password }), json);
  check(login, { 'login 200': (r) => r.status === 200 });
  const auth = { headers: { Authorization: `Bearer ${login.json('accessToken')}`, 'Content-Type': 'application/json' } };

  let items = http.get(`${API}/recipes?pageSize=20`, auth).json('items') || [];
  if (items.length === 0) {
    const published = http.get(`${API}/library?pageSize=1`).json('items') || [];
    if (published.length === 0) {
      throw new Error('No recipes and an empty library; load db/seed/test_data.sql or publish a recipe first.');
    }
    http.post(`${API}/library/${published[0].id}/copy`, null, auth);
    items = http.get(`${API}/recipes?pageSize=20`, auth).json('items') || [];
  }

  return { token: login.json('accessToken'), recipeIds: items.map((r) => r.id) };
}

export default function (data) {
  const params = (name) => ({ headers: { Authorization: `Bearer ${data.token}` }, tags: { name } });
  const recipeId = data.recipeIds[Math.floor(Math.random() * data.recipeIds.length)];

  group('workspace', () => {
    check(http.get(`${API}/recipes?pageSize=20`, params('recipes')), { 'recipes 200': (r) => r.status === 200 });
    check(http.get(`${API}/recipes/${recipeId}`, params('recipe')), { 'recipe 200': (r) => r.status === 200 });
  });

  group('calculator', () => {
    check(http.get(`${API}/recipes/${recipeId}/cost?loaves=3`, params('cost')), { 'cost 200': (r) => r.status === 200 });
    const start = new Date(Date.now() + 86400000).toISOString();
    check(http.get(`${API}/recipes/${recipeId}/timeline?start=${encodeURIComponent(start)}`, params('timeline')), {
      'timeline 200': (r) => r.status === 200,
    });
  });

  group('library', () => {
    check(http.get(`${API}/library?pageSize=20`, { tags: { name: 'library' } }), { 'library 200': (r) => r.status === 200 });
  });

  sleep(1);
}
