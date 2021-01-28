<template>
  <div>
    <h1>Files</h1>
    <div>
      <h2>Find files:</h2>
      <p>Taken
        <input type="number" min="1" step="1" max="4000" v-model="distance" placeholder="distance in miles" />
        miles from
        <input type="number" min="-180" max="180" step="0.00000001" v-model="latitude" placeholder="latitude">
        x
        <input type="number" min="-180" max="180" step="0.00000001" v-model="longitude" placeholder="longitude">
      </p>
      <p>Containing the word <input type="text" v-model="keyword" placeholder="keyword" /></p>
    </div>
    <div>Matching {{filteredFiles.length}} files</div>
    <div v-for="file in filteredFiles" :key="file.u">
      {{file.r}}
    </div>
  </div>
</template>

<script>
//import HelloWorld from './components/HelloWorld.vue'

export default {
  name: 'App',
  // components: {
  //   HelloWorld
  // }
  data: function() {
    return {
      files: [],
      distance: null,
      latitude: null,
      longitude: null,
      keyword: null
    }
  },
  computed: {
    filteredFiles: function() {
      const files = this.files, dstV = parseInt(this.distance), latV = parseFloat(this.latitude), lngV = parseFloat(this.longitude), kw = this.keyword
      const dst = isFinite(dstV) ? dstV : null
      const lat = isFinite(latV) ? latV : null
      const lng = isFinite(lngV) ? lngV : null
      var valid = []

      if (!(dst && lat && lng) && !kw) return files

      for (var i = 0; i < files.length; i++) {
        const file = files[i]
        const loc = file.l

        if (dst && lat && lng) {
          if (!loc || !loc.length) continue;

          const parts = loc.split(",")
          if (parts.length !== 2) continue;

          const flat = parseFloat(parts[0])
          const flng = parseFloat(parts[1])

          if (!isFinite(flat) || !isFinite(flng)) continue;

          const distance = this.getDistanceInMiles(lat, flat, lng, flng)
          if (distance > dst) continue;
        }
        if (kw && !((file.n && file.n.indexOf(kw) >= 0) || (file.t && file.t.indexOf(kw) >= 0) || (file.x && file.x.indexOf(kw) >= 0))) continue;

        valid.push(file);
      }

      return valid
    }
  },
  methods: {
    getDistanceInMiles: function(latitude1InDegrees, latitude2InDegrees, longitude1InDegrees, longitude2InDegrees)
    {
      // Uses the haversine formula to calculate the great-circle distance between two points
      const earthRadiusInMiles = 3958.8;
      const latitude1InRadians = latitude1InDegrees * Math.PI/180;
      const latitude2InRadians = latitude2InDegrees * Math.PI/180;
      const latitudeDeltaInRadians = (latitude2InDegrees - latitude1InDegrees) * Math.PI/180;
      const longitudeDeltaInRadians = (longitude1InDegrees - longitude2InDegrees) * Math.PI/180;

      const squareOfHalfChordLength = Math.sin(latitudeDeltaInRadians / 2) * Math.sin(latitudeDeltaInRadians / 2) +
            Math.cos(latitude1InRadians) * Math.cos(latitude2InRadians) *
            Math.sin(longitudeDeltaInRadians / 2) * Math.sin(longitudeDeltaInRadians / 2);
      const angularDistanceInRadians = 2 * Math.atan2(Math.sqrt(squareOfHalfChordLength), Math.sqrt(1 - squareOfHalfChordLength));

      const distanceInMiles = earthRadiusInMiles * angularDistanceInRadians;
      
      return distanceInMiles;
    }
  },
  mounted: async function() {
    const response = await fetch(process.env.VUE_APP_META_DATA_URL)
    const metaData = await response.json()
//    const discData = await fetch(process.env.VUE_APP_DISC_DATA_URL)
    this.files.splice(0, 0, ...metaData.files)
  }
}
</script>

<style>
#app {
  font-family: Avenir, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  text-align: center;
  color: #2c3e50;
  margin-top: 60px;
}
</style>
