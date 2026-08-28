FROM node:24.20.0-bookworm

WORKDIR /workspace/src/frontend

RUN npm install --global --allow-scripts=@pnpm/exe @pnpm/exe@12.0.0

COPY src/frontend/package.json src/frontend/pnpm-lock.yaml ./

RUN pnpm install --frozen-lockfile

COPY src/frontend/ ./

EXPOSE 5173

CMD ["pnpm", "dev", "--host", "0.0.0.0", "--port", "5173", "--strictPort"]
