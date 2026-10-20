# T10 frontend (Next.js). Stage-ovi po konvenciji repoa:
#   test    — lint, provera tipova i Vitest; JUnit rezultat u /results (profil 'tests', servis t10-tests-ui)
#   runtime — samostalan Next.js server na portu 3000, kao neprivilegovan korisnik (servis t10-ui)

FROM node:24-alpine AS base
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1

FROM base AS deps
COPY package.json package-lock.json ./
RUN npm ci

FROM deps AS source
COPY . .

FROM source AS test
ENV CI=true TEST_RESULTS_DIR=/results
CMD ["sh", "-c", "npm run lint && npm run typecheck && npm test"]

FROM source AS build
RUN npm run build

FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 PORT=3000 HOSTNAME=0.0.0.0
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static
COPY --from=build --chown=node:node /app/public ./public
USER node
EXPOSE 3000
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3000/health || exit 1
CMD ["node", "server.js"]
