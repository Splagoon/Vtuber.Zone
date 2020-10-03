<script lang="ts">
  import MainContainer from "../components/MainContainer.svelte";
  import Sidebar from "../components/Sidebar.svelte";
</script>

<style lang="scss">
  @import "../style/vars.scss";

  .floating-box {
    align-self: center;
    margin: auto;
    padding: $floating-margin;
    max-width: 20rem;

    h2 {
      text-align: center;
      font-size: 2rem;
      line-height: 2rem;
      font-weight: 200;
    }

    div {
      margin-top: 1rem;
    }

    a {
      color: $accent-color;
    }
  }

  .attributions div {
    margin-bottom: 1rem;
  }
</style>

<Sidebar page="about">
  <div class="attributions">
    <div>
      Illustrations &copy; <a href="https://www.irasutoya.com/">Irasutoya</a>
    </div>
    <div>
      YouTube/Twitch/Twitter icons &copy; <a href="https://www.iconfinder.com/abhishekpipalva">Abhishek Pipalva</a>
    </div>
    <div>
      Country flag icons &copy; <a href="https://www.flaticon.com/authors/freepik" title="Freepik">Freepik</a>
    </div>
    <div>
      The font used is <a href="https://fonts.google.com/specimen/Oswald">Oswald</a>
    </div>
  </div>
</Sidebar>

<MainContainer>
  <div class="floating-box">
    <h2>Welcome!</h2>
    <div>
      My name is <a href="https://twitter.com/Splagoon">Splagoon</a>, and I
      built Vtuber.Zone to help people discover and enjoy new Vtubers, large
      and small. I hope you find it useful!
    </div>
    <div>
      The full source code for this website is available on
      <a href="https://github.com/Splagoon/Vtuber.Zone">Github</a>.
      Contributions are welcome!
    </div>
  </div>
</MainContainer>
