<script lang="ts">
  import MainContainer from "../components/MainContainer.svelte";
  import GridContainer from "../components/GridContainer.svelte";
  import Sidebar from "../components/Sidebar.svelte";
  import Vtuber from "../components/Vtuber.svelte";
  import axios from "axios";

  const loadVtubers = (async () => {
    const res = await axios.get("https://vtuber.zone/api/vtubers");
    return res.data;
  })();
</script>

<Sidebar page="vtubers">
  <h2 class="row">
    {#await loadVtubers}
      Loading...
    {:then vtubers} 
      Currently tracking {vtubers.length} Vtubers
    {/await}
  </h2>
  <div>
    Know of a Vtuber not on this list?
    <a href="https://github.com/Splagoon/Vtuber.Zone/issues/new">Open an issue on Github</a>
    or reach out to me on <a href="https://twitter.com/Splagoon">Twitter</a>.
  </div>
</Sidebar>
<MainContainer>
  <GridContainer>
    {#await loadVtubers then vtubers}
      {#each vtubers as vtuber}
        <Vtuber {vtuber} />
      {/each}
    {/await}
  </GridContainer>
</MainContainer>
