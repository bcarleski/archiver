<template>
  <div>
    <h1>Files</h1>
    <div>
      <h2>Find files:</h2>
      <p>Taken
        <input type="number" min="1" step="1" max="4000" v-model="distance" placeholder="distance" />
        miles from
        <input type="number" min="-180" max="180" step="0.00000001" v-model="latitude" placeholder="latitude">
        x
        <input type="number" min="-180" max="180" step="0.00000001" v-model="longitude" placeholder="longitude">
      </p>
      <p>Containing the word <input type="text" v-model="keyword" placeholder="keyword" /></p>
    </div>
    <div>Matching {{filteredFiles.length}} files</div>
    <div class="grid-container">
      <div class="tree-container">
        <div v-for="file in filteredFiles" :key="file.u">
          <a @click.prevent.stop="selectedFile = file" href="#">{{file.r}}</a>
        </div>
      </div>
      <div class="detail-container">
        <div v-if="selectedFile">
          <a :href="selectedFile.r" target="_blank" v-if="currentDisc === selectedFile.c">
            <img :src="selectedFile.r" v-if="selectedFileIsImage" class="preview-image" />
            <span v-else>{{selectedFile.n}}</span>
          </a>
          <table>
            <tbody>
              <tr v-for="field in fields" :key="field.key">
                <th>{{field.value}}</th>
                <td>
                  <a target="_blank" :href="'https://www.google.com/maps/@' + selectedFile['l'] + ',15z'" v-if="field.key === 'l' && selectedFile.l && selectedFile.l.length">{{selectedFile.l}}</a>
                  <span v-else-if="field.key === 'd' && !isNaN(parseInt(selectedFile.d)) && parseInt(selectedFile.d)">{{new Date(parseInt(selectedFile.d)).toString()}}</span>
                  <span v-else-if="field.key === 'p' && selectedFile.p && selectedFile.p.length">{{selectedFile.p.join(', ')}}</span>
                  <span v-else>{{selectedFile[field.key]}}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
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
      fields: [],
      distance: null,
      latitude: null,
      longitude: null,
      keyword: null,
      selectedFile: null,
      currentDisc: null
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
        if (kw &&
          !(
            (file.n && file.n.indexOf(kw) >= 0) ||
            (file.t && file.t.indexOf(kw) >= 0) ||
            (file.x && file.x.indexOf(kw) >= 0) ||
            (file.p && file.p.filter(x => x.indexOf(kw) >= 0).length > 0)
          )) continue;

        valid.push(file);
      }

      return valid
    },
    selectedFileIsImage: function() {
      const name = this.selectedFile.n
      return /\.(jpg|jpeg|png|bmp|gif|webp|tiff)$/.test(name.toLowerCase())
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
  mounted: function() {
    if (window.archiverMetaData) {
      this.files.splice(0, 0, ...window.archiverMetaData.files)

      for (var key in window.archiverMetaData.fieldMeaning) {
        this.fields.push({key, value:window.archiverMetaData.fieldMeaning[key]})
      }
    }
    if (window.archiverDiscMetaData) this.currentDisc = window.archiverDiscMetaData.discNumber
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

.grid-container {
    display: grid;
    grid-template-columns: 1fr 1fr;
    grid-gap: 20px;
}

th, td {
  text-align: left;
}

.preview-image {
  max-width: 400px;
  max-height: 400px;
}
</style>
