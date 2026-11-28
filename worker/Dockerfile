FROM node:22-slim
ARG TARGETARCH
ARG GH_VERSION=2.103.3
ARG PLAYWRIGHT_VERSION=1.65.0
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL "https://github.com/cli/cli/releases/download/v${GH_VERSION}/gh_${GH_VERSION}_linux_${TARGETARCH}.tar.gz" \
    | tar -xz -C /tmp \
 && mv "/tmp/gh_${GH_VERSION}_linux_${TARGETARCH}/bin/gh" /usr/local/bin/gh \
 && rm -rf "/tmp/gh_${GH_VERSION}_linux_${TARGETARCH}"
RUN npm install -g "playwright@${PLAYWRIGHT_VERSION}" \
 && playwright install --with-deps --only-shell chromium \
 && rm -rf /var/lib/apt/lists/* \
 && chmod -R a+rX "$PLAYWRIGHT_BROWSERS_PATH"
RUN npm install -g @anthropic-ai/claude-code@2.1.332
RUN useradd -m worker && mkdir /claude /work && chown worker /claude /work
USER worker
ENV CLAUDE_CONFIG_DIR=/claude
WORKDIR /work
