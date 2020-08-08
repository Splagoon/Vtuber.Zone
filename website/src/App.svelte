<script lang="ts">
  import Stream from "./Stream.svelte";
  import axios from "axios";

  let streams = [];

  async function load() {
    const res = await axios.get(
      "https://vtuber.zone/api/streams/live?by=start-time"
    );
    streams = res.data;
  }

  load();
  setInterval(load, 60 * 1000);

  import { quintOut } from "svelte/easing";
  import { crossfade } from "svelte/transition";
  import { flip } from "svelte/animate";

  const [send, receive] = crossfade({
    duration: 500,
    fallback(node, params) {
      const style = getComputedStyle(node);
      const transform = style.transform === "none" ? "" : style.transform;

      return {
        duration: 500,
        easing: quintOut,
        css: (t) => `
					transform: ${transform} scale(${t});
					opacity: ${t}
				`,
      };
    },
  });

  document.addEventListener('keydown', (event) => {
    if(event.keyCode == 37) {
      streams = streams.slice(0, streams.length-1);
    } else if (event.keyCode == 39) {
      const stream = { ...streams[0] };
      stream.url += Math.random();
      streams = [stream, ...streams];
    } else if (event.keyCode == 38) {
      streams = streams.sort((a, b) => Math.random());
    }
});
</script>

<style lang="scss">
  @import "./style/vars.scss";

  @for $i from 1 through 8 {
    $container-width: $i * ($stream-box-width + 2rem);
    @media only screen and (min-width: ($container-width / 1rem) * $one-rem-in-px) {
      .stream-container {
        width: $container-width;
      }
    }
  }

  .stream-container {
    display: flex;
    flex-wrap: wrap;
    margin: auto;
  }
</style>

<main>
  <div class="stream-container">
    {#each streams as stream (stream.url)}
      <div
        in:receive={{ key: stream.url }}
        out:send={{ key: stream.url }}
        animate:flip={{ duration: 500 }}>
        <Stream {stream} />
      </div>
    {/each}
  </div>
</main>
