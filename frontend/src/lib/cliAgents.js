// CLI agent configuration snippets for the dashboard "CLI Tool" tab.
//
// Data-driven: each agent carries metadata (name, status chip, description)
// and `buildConfig` renders its snippet blocks reactively from the current
// endpoint / API key / example model. Placeholders in the SPEC are replaced
// inline via template literals — there is no post-hoc string substitution.

/** Fallback example model id when /models is unavailable. */
export const DEFAULT_EXAMPLE_MODEL = 'openai-primary/gpt-4o-mini';

/**
 * Agent metadata for the five cards rendered by CliTool.svelte.
 * chip.tone: 'ok' (green) | 'warn' (amber) | 'muted' (grey).
 */
export const AGENTS = [
  {
    id: 'claude-code',
    name: 'Claude Code',
    chip: { label: 'Supported', tone: 'ok' },
    description: 'Point Claude Code at MiniRouter via environment variables (or a persistent settings.json).'
  },
  {
    id: 'codex',
    name: 'Codex CLI',
    chip: { label: 'Needs Responses API', tone: 'warn' },
    description: 'Codex CLI needs a model provider entry in config.toml — and a Responses API adapter on the router side.'
  },
  {
    id: 'opencode',
    name: 'OpenCode',
    chip: { label: 'Supported', tone: 'ok' },
    description: 'OpenCode talks OpenAI-compatible through @ai-sdk/openai-compatible.'
  },
  {
    id: 'aider',
    name: 'Aider',
    chip: { label: 'Supported', tone: 'ok' },
    description: 'Aider uses its OpenAI-compatible client with a model prefix.'
  },
  {
    id: 'gemini',
    name: 'Gemini CLI',
    chip: { label: 'Not supported', tone: 'muted' },
    description: 'Gemini CLI cannot target OpenAI-compatible endpoints today.',
    bodyText: "Official Gemini CLI docs expose no custom OpenAI-compatible base URL (no GOOGLE_GEMINI_BASE_URL; CODE_ASSIST_ENDPOINT speaks Google's Code Assist protocol). Pointing it at MiniRouter is not currently possible (open feature request upstream)."
  }
];

/**
 * Build the snippet blocks for one agent.
 *
 * @param {string} agentId one of the AGENTS ids
 * @param {{ baseUrl: string, apiKey: string, exampleModel: string }} opts
 * @returns {Array<{label: string, language: string, code: string, note?: string}>}
 */
export function buildConfig(agentId, { baseUrl, apiKey, exampleModel }) {
  switch (agentId) {
    case 'claude-code':
      return [
        {
          label: 'Shell',
          language: 'bash',
          code: `export ANTHROPIC_BASE_URL="${baseUrl}"            # MiniRouter root — no /v1; Claude Code appends /v1/messages
export ANTHROPIC_AUTH_TOKEN="${apiKey}"              # sent as Authorization: Bearer (ANTHROPIC_API_KEY sends x-api-key → rejected)
export ANTHROPIC_DEFAULT_SONNET_MODEL="${exampleModel}"  # optional; also _OPUS_ / _HAIKU_ variants`
        },
        {
          label: 'Persistent alternative (~/.claude/settings.json)',
          language: 'json',
          code: `{ "env": { "ANTHROPIC_BASE_URL": "${baseUrl}", "ANTHROPIC_AUTH_TOKEN": "${apiKey}" } }`
        }
      ];
    case 'codex':
      return [
        {
          label: '~/.codex/config.toml (user-level only; ignored project-local)',
          language: 'toml',
          code: `model_provider = "minirouter"
model = "${exampleModel}"

[model_providers.minirouter]
name = "MiniRouter"
base_url = "${baseUrl}/v1"
env_key = "MINIROUTER_API_KEY"
wire_api = "responses"`,
          note: 'Codex (current versions) only supports wire_api = "responses" and POSTs to /v1/responses — MiniRouter implements /v1/chat/completions and /v1/messages only, so this needs a Responses API adapter in MiniRouter before it works.'
        }
      ];
    case 'opencode':
      return [
        {
          label: 'opencode.json (project or ~/.config/opencode/opencode.json)',
          language: 'json',
          code: `{
  "$schema": "https://opencode.ai/config.json",
  "provider": { "minirouter": { "npm": "@ai-sdk/openai-compatible",
    "options": { "baseURL": "${baseUrl}/v1", "apiKey": "${apiKey}" },
    "models": { "${exampleModel}": {} } } },
  "model": "minirouter/${exampleModel}"
}`
        }
      ];
    case 'aider':
      return [
        {
          label: '.env (or exports)',
          language: 'bash',
          code: `OPENAI_API_BASE=${baseUrl}/v1
OPENAI_API_KEY=${apiKey}`
        },
        {
          label: 'Shell',
          language: 'bash',
          code: `aider --model openai/${exampleModel}`,
          note: "The leading `openai/` is Aider's literal prefix for its OpenAI-compatible client. Aider's /v1 handling varies across versions (upstream issue #4027) — if 404, try base URL with and without trailing /v1."
        }
      ];
    case 'gemini':
      // No snippet: Gemini CLI cannot be pointed at MiniRouter today.
      return [];
    default:
      return [];
  }
}
