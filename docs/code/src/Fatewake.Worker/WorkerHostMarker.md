# WorkerHostMarker

## Purpose
Marks the dedicated Fatewake background worker application in code and documentation. The worker host processes durable jobs independently from API and Web replicas.

## Usage
The worker executable registers PostgreSQL work persistence, RabbitMQ signaling, queue concurrency options, and DurableWorkWorker. Kubernetes can scale this project independently from request-serving applications.

## Deployment
Run one or more replicas on Linux containers or Windows hosts. Queue concurrency is configured per process, so total cluster concurrency is approximately replica count multiplied by configured slots.

## Correctness
Worker process lifetime is never authoritative. PostgreSQL leases and durable job/step rows provide recovery after crashes, eviction, deployment, or broker interruption.
