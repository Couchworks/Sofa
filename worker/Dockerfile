FROM node:22-slim
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*
RUN npm install -g @anthropic-ai/claude-code@2.1.332
RUN useradd -m worker && mkdir /claude /work && chown worker /claude /work
USER worker
ENV CLAUDE_CONFIG_DIR=/claude
WORKDIR /work
