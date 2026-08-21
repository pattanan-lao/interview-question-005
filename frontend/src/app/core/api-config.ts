// Relative on purpose: in Docker, nginx serves the app and reverse-proxies
// /api to the backend container; in local dev, proxy.conf.json forwards /api
// to http://localhost:5080. Same-origin either way, so no CORS and no
// backend hostname baked into the production bundle.
export const API_BASE_URL = '/api';
