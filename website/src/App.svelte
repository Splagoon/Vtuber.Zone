<script>
  import page from "page";

  import Streams from "./routes/Streams.svelte";
  import Vtubers from "./routes/Vtubers.svelte";
  import About from "./routes/About.svelte";

  let root;
  let params;

  page("/", () => (root = Streams));
  page("/vtubers", () => (root = Vtubers));
  page("/about", () => (root = About));

  page.start();
</script>

<svelte:component this={root} {params} />
