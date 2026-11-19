# SceneScheduler

Creates the Pending `generated_scene` row and durable job in one transaction (job key includes timeline, sequence and a retry generation). A concurrent reservation loses the unique-index race and returns the winner.
