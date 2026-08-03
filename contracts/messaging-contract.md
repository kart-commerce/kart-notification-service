# Messaging Contract: kart-notification-service

Human-readable index alongside `message-bus-manifest.json` (the machine-readable source of truth
loaded at startup by `Kart.Shared.Messaging`). See `README.md` in this folder for why this
manifest's JSON *shape* differs from the illustrative one in `kart-platform`'s docs repo while its
*content* (every exchange, queue, binding, DLQ) is identical.

Own exchange: `notification.exchange` (topic, durable). Own DLX: `notification.dlx` (topic,
durable). Notification is the platform's broadest single fan-in consumer (ADR-0003) — it binds its
own queues directly onto **seven other services' exchanges**, never a shared fan-in exchange.

## Consumed events (13, across 3 criticality tiers)

| Event | Routing key | Publisher | Queue | Tier | Max attempts |
|---|---|---|---|---|---|
| `OrderCreated` | `order.order.created` | kart-order-service | `notification.order-events.queue` | 1 | 5 (+ paged) |
| `PaymentCompleted` | `payment.intent.completed` | kart-payment-service | `notification.payment-events.queue` | 1 | 5 (+ paged) |
| `PaymentFailed` | `payment.intent.failed` | kart-payment-service | `notification.payment-events.queue` | 1 | 5 (+ paged) |
| `RefundIssued` | `payment.refund.issued` | kart-payment-service | `notification.payment-events.queue` | 1 | 5 (+ paged) |
| `OrderConfirmed` | `order.order.confirmed` | kart-order-service | `notification.order-events.queue` | 2 | 3 |
| `OrderCancelled` | `order.order.cancelled` | kart-order-service | `notification.order-events.queue` | 2 | 3 |
| `OrderCompensationTriggered` | `order.order.compensation-triggered` | kart-order-service | `notification.order-events.queue` | 2 | 3 |
| `OrderDelivered` | `order.order.delivered` | kart-order-service | `notification.order-events.queue` | 2 | 3 |
| `ShipmentDispatched` | `shipping.shipment.dispatched` | kart-shipping-service | `notification.shipping-events.queue` | 2 | 3 |
| `DeliveryStatusUpdated` | `tracking.delivery-status.updated` | kart-delivery-tracking-service | `notification.tracking-events.queue` | 2 | 3 |
| `UserRegistered` | `identity.user.registered` | kart-identity-service | `notification.identity-events.queue` | 2 | 3 |
| `WishlistPriceAlertTriggered` | `wishlist.price-alert.triggered` | kart-wishlist-service | `notification.wishlist-events.queue` | 3 | 2 |
| `UserNotificationPreferenceUpdated` | `user.notification-preference-updated` | kart-user-service | `notification.user-events.queue` | 3 | 2 |

Two queues (`order-events`, `payment-events`) carry events from more than one criticality tier
(wildcard bindings `order.#` / `payment.#` — the topic-exchange `#` wildcard, matching
zero-or-more words, not `*`, which matches exactly one word and would never match a 3-segment
routing key like `order.order.created`; this exact bug shipped in kart-platform's own illustrative
manifest.json and was only caught by a live end-to-end RabbitMQ test) — the retry ceiling is
enforced **per event type** (`Kart.Notification.Domain.Catalog.TriggeringEventCatalog`), not by
the queue's own ladder depth.
See `README.md` for why this service's consumers do not use `Kart.Shared.Messaging`'s generic
`RabbitMqConsumerHostedServiceBase` retry routing.

## Published event

| Event | Routing key | Consumer | Payload | Retry |
|---|---|---|---|---|
| `NotificationSent` | `notification.notification.sent` | kart-analytics-service | `userId, channel, status` | 1x, fire-and-forget, no DLQ, no Outbox (published directly right after the row's terminal status `UPDATE` commits) |

## `userId` resolution (ADR-0020)

9 of the 13 consumed events do not carry `userId` directly. Two local, non-aggregate lookup
projections resolve it: `order_user_index` (`orderId → userId`, seeded from `OrderCreated`) and
`tracking_order_index` (`trackingId → orderId`, seeded from `ShipmentDispatched`). A lookup miss
is transient (the seeding event hasn't been consumed yet) and requeues onto the dependent event's
own already-modeled retry ladder — not a special mechanism.
