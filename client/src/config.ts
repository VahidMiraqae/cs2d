// Dev: Vite on :5173/5174 talks to the API on :5275 of the same host. Production: the server hosts the client, so use the same origin.
export const BASE = import.meta.env.DEV ? `http://${location.hostname}:5275` : ''
