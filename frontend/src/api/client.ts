import createClient from 'openapi-fetch'
import type { paths } from './schema'

// Contract paths already include /api/v1; keep the base URL at the origin root.
export const api = createClient<paths>({ baseUrl: '/' })
