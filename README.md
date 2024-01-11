## Coordinating Distributed Marten Rebuilds

This project serves as example for dealing with marten rebuilds in a multi-node environment (Daemon HotCold mode) whereby offline rebuilds
are impractical.

In this environment, any one of the nodes could be holding the daemon lock, and a web request requesting a rebuild may not hit the correct node. 
To get around this, we forward the rebuild request to all nodes via RabbitMQ, and the node with the lock will stop the currently running daemon and start a rebuild.

After the rebuild kicks off, its state is communicated via a distributed cache to all nodes, with middleware blocking any writes 
into the system whilst the rebuilding is occuring (I recommend combining this with a maintenance page for long rebuilds). 
This state also provides a way for you to show the current rebuild state via your own UI.

This was built to fit into our existing technology stack using libraries we're familiar with, 
intentionally avoiding the need to bring in a dedicated distributed coordinator (ie actor framework)

The cores pieces are:
- MassTransit connected to RabbitMQ with an exclusive, uniquely named queue per node (any messaging abstraction is fine).
- FusionCache providing a pub/sub distributed cache that can communicate our rebuild state across all nodes.
- ASP.NET Core middleware that blocks Post/Delete/Patch/Put methods into the system.

### Limitations
- The "rebuild running" state is set to expire after x minutes (currently 10) so the cache returns to a good state after something goes wrong.
  If a single projection takes longer than this, you should increase this value.
- Some rebuild errors do not get bubbled up or reported to an attached listener, 
  so the UI rebuild state may show a successful rebuild occurred, when in fact it failed.
- This is unlikely to work with multi-database configurations.

### Future
- A wolverine implementation would be useful.

### Running
1. `docker-compose up`
2. `dotnet run --urls=http://localhost:5011/`
3. `dotnet run --urls=http://localhost:5012/` (this order is important - the UI is configured to connect to this node)
4. `rebuild-ui` folder -> 'npm i' -> 'npm run dev'
5. Use the UI to seed some data, then you should be good to test a rebuild. 
6. You can test the middleware by attempting to seed an entity during a rebuild, your console should show a 503 exception.