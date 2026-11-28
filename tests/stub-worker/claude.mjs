#!/usr/bin/env node
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { lookup } from "node:dns/promises";
import { connect } from "node:net";
import { deflateSync } from "node:zlib";
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
  tools: ["Task", "WebFetch", "WebSearch", ...allowedTools],
  mcp_servers: [{ name: "sofa", status: "connected" }],
  stub: true,
});
print({ type: "system", subtype: "thinking_tokens", tokens: 0 });
print({ type: "assistant", message: { content: [{ type: "text", text: `Read the Brief (${brief.length} characters).` }] } });

const said = /STUB: say (\S+)/.exec(brief);
if (said) {
  print({ type: "assistant", message: { content: [
    { type: "text", text: `The planted secret is ${said[1]}.` },
    { type: "tool_use", id: "toolu_say", name: "Bash", input: { command: `echo ${said[1]}` } },
  ] } });
  print({ type: "user", message: { content: [{ type: "tool_result", tool_use_id: "toolu_say", content: said[1] }] } });
}

function repositoryToken() {
  const file = "/tmp/sofa/github-token";
  return existsSync(file) ? readFileSync(file, "utf8") : process.env.GH_TOKEN;
}

if (brief.includes("STUB: tell its secrets")) {
  print({ type: "assistant", message: { content: [{ type: "text",
    text: `GH_TOKEN=${repositoryToken() ?? "none"} ANTHROPIC_API_KEY=${process.env.ANTHROPIC_API_KEY ?? "none"}` }] } });
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

const toolUse = (name, input) => print({ type: "assistant", message: { content: [{ type: "tool_use", name: `mcp__sofa__${name}`, input }] } });
const toolResult = (answer) =>
  print({ type: "user", message: { content: [{ type: "tool_result", content: answer.error ?? answer.result?.content?.[0]?.text, is_error: Boolean(answer.error || answer.result?.isError) }] } });
const callTool = async (name, input) => {
  toolUse(name, input);
  const answer = await call("tools/call", { name, arguments: input });
  toolResult(answer);
  return answer;
};

const crc32 = (bytes) => {
  let crc = ~0;
  for (const byte of bytes) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return ~crc >>> 0;
};
const chunk = (type, data) => {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const typed = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(typed));
  return Buffer.concat([length, typed, crc]);
};
const onePixelPng = () => {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(1, 0);
  header.writeUInt32BE(1, 4);
  header.set([8, 2, 0, 0, 0], 8);
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", header),
    chunk("IDAT", deflateSync(Buffer.from([0, 0x2b, 0x2b, 0x2b]))),
    chunk("IEND", Buffer.alloc(0)),
  ]);
};

const initialized = await call("initialize", {
  protocolVersion: "2025-06-18",
  capabilities: {},
  clientInfo: { name: "claude-code-stub", version: "0.0.0" },
});
let ended = initialized;
if (!initialized.error) {
  const listed = await call("tools/list", {});
  print({ type: "system", subtype: "stub_tools_offered", content: listed.error ?? (listed.result?.tools ?? []).map((tool) => tool.name).join(", ") });
  const workItem = await callTool("read_work_item", {});
  const workItemText = workItem.result?.content?.[0]?.text ?? "";

  const askingOnce = /STUB: ask once (.+)/.exec(brief);
  const asking = askingOnce
    ? (workItemText.includes("**Answered") ? null : askingOnce)
    : /STUB: ask (?!once )(.+)/.exec(brief);
  const outcome = /STUB: outcome (\S+)/.exec(brief)?.[1] ?? /^- `([^`]+)`:/m.exec(brief)?.[1];
  const attachments = [];
  if (brief.includes("STUB: attach a screenshot")) {
    writeFileSync("/work/screenshot.png", onePixelPng());
    attachments.push("screenshot.png");
  }

  ended = asking
    ? await callTool("ask", { question: asking[1].trim() })
    : await callTool("finish", {
        outcome,
        contribution: "## What the stub did\n\nIt played back a recorded Run.",
        attachments,
      });

  if (!ended.error && !ended.result?.isError) {
    const afterTheEnd = await call("tools/call", { name: "read_work_item", arguments: {} });
    print({ type: "system", subtype: "stub_called_after_the_end", content: afterTheEnd.error ?? afterTheEnd.result?.content?.[0]?.text });
  }
}

const failed = Boolean(ended.error || ended.result?.isError);
print({ type: "result", subtype: failed ? "error_during_execution" : "success", is_error: failed, num_turns: 3 });
process.exit(failed ? 1 : 0);
