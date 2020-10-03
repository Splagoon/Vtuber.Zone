<style lang="scss">
  @import "../style/vars.scss";

  div {
    display: flex;
    flex-wrap: wrap;
    margin: 0 auto;
    align-content: flex-start;
    width: $full-box-width;
  }

  @for $i from 2 through 8 {
    $container-width: $i * $full-box-width;
    @media only screen and (orientation: landscape) and (min-width: ($container-width + $sidebar-width) / 1rem * $one-rem-in-px),
      only screen and (orientation: portrait) and (min-width: $container-width / 1rem * $one-rem-in-px) {
      div {
        width: $container-width;
      }
    }
  }
</style>

<div>
  <slot />
</div>
