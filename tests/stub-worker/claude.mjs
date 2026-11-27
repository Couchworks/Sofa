#!/usr/bin/env node
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { lookup } from "node:dns/promises";
import { connect } from "node:net";
import { setTimeout as sleep } from "node:timers/promises";

const args = process.argv.slice(2);
const signedInMarker = `${process.env.CLAUDE_CONFIG_DIR ?? "/claude"}/.stub-signed-in`;
const print = (line) => process.stdout.write(`${JSON.stringify(line)}\n`);
const valueOf = (flag) => (args.includes(flag) ? args[args.indexOf(flag) + 1] : undefined);
const valuesOf = (flag) => {
  const start = args.indexOf(flag);
  if (start < 0) return [];
  const values = [];
  for (const value of args.slice(start + 1)) {
    if (value.startsWith("--")) break;
    values.push(value);
  }
  return values;
};

if (args[0] === "--version") {
  console.log("0.0.0 (stub of Claude Code)");
  process.exit(0);
}

if (args[0] === "auth" && args[1] === "status") {
  const signedIn = existsSync(signedInMarker);
  console.log(JSON.stringify(signedIn
    ? { loggedIn: true, authMethod: "claude.ai", loginExpiresAt: "2026-12-24T10:00:00.000Z", email: "stub@example.com" }
    : { loggedIn: false, authMethod: "none", loginExpiresAt: null }, null, 2));
  process.exit(signedIn ? 0 : 1);
}

if (args[0] === "sign-in-on-the-volume") {
  writeFileSync(signedInMarker, "stub");
  process.exit(0);
}

if (!args.includes("-p")) {
  console.error(`The stub of Claude Code doesn't know: ${args.join(" ")}`);
  process.exit(2);
}

if (args.includes("--bare")) {
  console.error("--bare ignores a subscription's sign-in");
  process.exit(2);
}

const brief = args[args.indexOf("-p") + 1];
const model = valueOf("--model");
const allowedTools = valuesOf("--allowedTools");
const mcp = JSON.parse(readFileSync(valueOf("--mcp-config"), "utf8")).mcpServers.sofa;

print({
  type: "system",
  subtype: "init",
  model,
  apiKeySource: process.env.ANTHROPIC_API_KEY ? "ANTHROPIC_API_KEY" : "none",
  tools: allowedTools,
  mcp_servers: [{ name: "sofa", status: "connected" }],
  stub: true,
});
print({ type: "assistant", message: { content: [{ type: "text", text: `Read the Brief (${brief.length} characters).` }] } });

const said = /STUB: say (\S+)/.exec(brief);
if (said) {
  print({ type: "assistant", message: { content: [
    { type: "text", text: `The planted secret is ${said[1]}.` },
    { type: "tool_use", id: "toolu_say", name: "Bash", input: { command: `echo ${said[1]}` } },
  ] } });
  print({ type: "user", message: { content: [{ type: "tool_result", tool_use_id: "toolu_say", content: said[1] }] } });
}

if (brief.includes("STUB: tell its secrets")) {
  print({ type: "assistant", message: { content: [{ type: "text",
    text: `GH_TOKEN=${process.env.GH_TOKEN ?? "none"} ANTHROPIC_API_KEY=${process.env.ANTHROPIC_API_KEY ?? "none"}` }] } });
}

const reach = (host, port) => new Promise((resolve) => {
  const socket = connect({ host, port, timeout: 3000 });
  socket.on("connect", () => { socket.destroy(); resolve("reached"); });
  socket.on("timeout", () => { socket.destroy(); resolve("failed (timeout)"); });
  socket.on("error", (error) => resolve(`failed (${error.code})`));
});

const probe = async (target) => {
  if (target.startsWith("https://")) {
    try {
      const response = await fetch(target, { method: "HEAD", signal: AbortSignal.timeout(10000) });
      return `reached (${response.status})`;
    } catch (error) {
      return `failed (${error.cause?.code ?? error.name})`;
    }
  }
  const [, name, port] = /^tcp:(.+):(\d+)$/.exec(target) ?? [];
  if (!name) return "failed (not a target)";
  const host = name.startsWith("@") ? (await lookup(name.slice(1)).catch(() => ({ address: name.slice(1) }))).address : name;
  return reach(host, Number(port));
};

const probes = /STUB: probe ([^\n]+)/.exec(brief);
if (probes) {
  for (const target of probes[1].trim().split(/\s+/)) {
    print({ type: "assistant", message: { content: [{ type: "text", text: `probe ${target}: ${await probe(target)}` }] } });
  }
}

if (brief.includes("STUB: hang")) {
  await sleep(24 * 60 * 60 * 1000);
}

const takes = /STUB: take (\d+)s/.exec(brief);
if (takes) {
  await sleep(Number(takes[1]) * 1000);
}

let jsonRpcId = 0;
const call = async (method, params) => {
  const response = await fetch(mcp.url, {
    method: "POST",
    headers: { ...mcp.headers, "content-type": "application/json", accept: "application/json, text/event-stream" },
    body: JSON.stringify({ jsonrpc: "2.0", id: ++jsonRpcId, method, params }),
  });
  const body = await response.text();
  if (!response.ok) return { error: `${response.status} ${body}` };
  const json = response.headers.get("content-type")?.includes("text/event-stream")
    ? body.split("\n").filter((line) => line.startsWith("data:")).map((line) => JSON.parse(line.slice(5))).find((m) => m.id === jsonRpcId)
    : JSON.parse(body);
  return json.error ? { error: JSON.stringify(json.error) } : { result: json.result };
};

const initialized = await call("initialize", {
  protocolVersion: "2025-06-18",
  capabilities: {},
  clientInfo: { name: "claude-code-stub", version: "0.0.0" },
});
const ended = initialized.error
  ? initialized
  : await call("tools/call", { name: "end_test_run", arguments: { summary: "The stub played back a recorded Run." } });

print({ type: "assistant", message: { content: [{ type: "tool_use", name: "mcp__sofa__end_test_run", input: {} }] } });
print({ type: "user", message: { content: [{ type: "tool_result", content: ended.error ?? ended.result?.content?.[0]?.text }] } });
print({ type: "result", subtype: ended.error ? "error_during_execution" : "success", is_error: Boolean(ended.error), num_turns: 2 });
process.exit(ended.error ? 1 : 0);
