[![progress-banner](https://backend.codecrafters.io/progress/redis/6b8092e7-2d9c-4e4b-bc5a-774e0c4750d3)](https://app.codecrafters.io/users/codecrafters-bot?r=2qF)

This is a starting point for C# solutions to the
["Build Your Own Redis" Challenge](https://codecrafters.io/challenges/redis).

In this challenge, you'll build a toy Redis clone that's capable of handling
basic commands like `PING`, `SET` and `GET`. Along the way we'll learn about
event loops, the Redis protocol and more.

**Note**: If you're viewing this repo on GitHub, head over to
[codecrafters.io](https://codecrafters.io) to try the challenge.

# Passing the first stage

The entry point for your Redis implementation is in `src/Server.cs`. Study and
uncomment the relevant code, and push your changes to pass the first stage:

```sh
git commit -am "pass 1st stage" # any msg
git push origin master
```

That's all!

# Stage 2 & beyond

Note: This section is for stages 2 and beyond.

1. Ensure you have `dotnet (8.0)` installed locally
1. Run `./your_program.sh` to run your Redis server, which is implemented in
   `src/Server.cs`.
1. Commit your changes and run `git push origin master` to submit your solution
   to CodeCrafters. Test output will be streamed to your terminal.

# Redis Replication
Leader-Follower architecture, (master replica)
replica instances are exact copies of the master instance
the replicas  reconnect to the master every time the connection is broken
the replica tries to be the exact copy of the master regardless of what happens to the master

## Mechanism that keeps the instances in sync
1. When a master and a replica instance are well-connected, the master keeps the replica updated by sending a stream of commands to the replica to replicate the effects on the dataset
2. When the link between the master and the replica breaks, for network issues or because a timeout is sensed in the master or the replica, the replica reconnects and attempts to proceed with a partial resynchronization: it means that it will try to just obtain the part of the stream of commands it missed during the disconnection.
3. When a partial resynchronization is not possible, the replica will ask for a full resynchronization. the master makes a snapshot of all the data and send it to the replica, followed by the stream of commands after the snapshot

Redis replicas asynchronously acknowledge the amount of data they receive periodically with the master. So the master does not wait every time for a command to be processed by the replicas, however it knows, if needed, what replica already processed what command. This allows having optional synchronous replication.

Synchronous replication of certain data can be requested by the clients using the [`WAIT`](https://redis.io/commands/wait) command.

- Replicas are able to accept connections from other replicas. Aside from connecting a number of replicas to the same master, replicas can also be connected to other replicas in a cascading-like structure. Since Redis 4.0, all the sub-replicas will receive exactly the same replication stream from the master.

- Redis replication is non-blocking on the master side. This means that the master will continue to handle queries when one or more replicas perform the initial synchronization or a partial resynchronization.
