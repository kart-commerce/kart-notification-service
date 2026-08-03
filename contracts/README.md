# contracts/

`api-contract.yaml` and this service's business content (exchanges, queues, bindings, DLQs, retry
tiers) are synced from `kart-platform/docs/services/kart-notification-service/` — the approved
design record — and should not be hand-edited for business-content changes; a topology or contract
change starts upstream in that repo.

**One deliberate exception:** `message-bus-manifest.json`'s *JSON shape* here is **not** a byte-for-
byte copy of `kart-platform`'s own manifest file. That file predates — and doesn't match — the
schema this platform's actual `Kart.Shared.Messaging` library (`MessageBusManifestLoader`/
`MessageBusManifest`) deserializes (confirmed against its source and against
`kart-identity-service`/`kart-payment-service`'s own working manifests: `queues[].deadLetter
{exchange,routingKey}` + `queues[].retryLadder{requeueTo,tiers[]}`, nested per-queue — not the
top-level `dlqs`/`retry` arrays with raw RabbitMQ `arguments` the docs-repo file uses). This file
reproduces every exchange, queue, binding, and DLQ the docs repo specifies, translated into the
schema the shared library actually loads, so the manifest is loadable at all. See
`messaging-contract.md` for the full content index.

**A second, related fix:** the docs repo's `order.exchange`/`payment.exchange` bindings use the
topic-exchange wildcard `*` (`order.*`), which matches exactly one word — it never matches a
3-segment routing key like `order.order.created` (`service.entity.action`,
naming-conventions.md). This file uses `#` instead (`order.#`, `payment.#` — zero-or-more words),
which is the wildcard that actually matches. This was caught by a live RabbitMQ end-to-end test
(publish `OrderCreated`, confirm a row lands in `notification_attempts`), not by unit/schema
tests alone — `MessageBusManifestContractTests.Multi_tier_queue_bindings_use_the_hash_wildcard_not_the_single_segment_star`
is a regression guard against it recurring.
