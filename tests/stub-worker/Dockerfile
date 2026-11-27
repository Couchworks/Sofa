FROM node:22-slim
COPY claude.mjs /usr/local/bin/claude
RUN chmod +x /usr/local/bin/claude && useradd -m worker && mkdir /claude /work && chown worker /claude /work
USER worker
ENV CLAUDE_CONFIG_DIR=/claude
WORKDIR /work
