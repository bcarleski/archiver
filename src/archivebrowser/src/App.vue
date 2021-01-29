<template>
  <div>
    <h1>Files</h1>
    <div>
      Find files taken
        <input type="number" min="1" step="1" max="4000" v-model="distance" placeholder="distance" @blur="recomputeFilteredFiles" />
        miles from
        <input type="number" min="-180" max="180" step="0.00000001" v-model="latitude" placeholder="latitude" @blur="recomputeFilteredFiles" />
        x
        <input type="number" min="-180" max="180" step="0.00000001" v-model="longitude" placeholder="longitude" @blur="recomputeFilteredFiles" />
        containing the word
        <input type="text" v-model="keyword" placeholder="keyword" @blur="recomputeFilteredFiles" />
        between
        <input type="datetime" v-model="startTime" placeholder="earliest date/time" @blur="recomputeFilteredFiles" />
        and
        <input type="datetime" v-model="endTime" placeholder="latest date/time" @blur="recomputeFilteredFiles" />
    </div>
    <div>Matching {{filteredFiles.length.toLocaleString()}} of {{files.length.toLocaleString()}} files</div>
    <div class="grid-container">
      <div class="tree-container">
        <ul>
          <tree-item class="item" v-for="(root, index) in filteredTreeRoots" :key="index" :item="root" @file-selected="selectFile"></tree-item>
        </ul>
      </div>
      <div class="detail-container">
        <file-detail :item="selectedFile" :fields="fields" v-if="selectedFile" />
      </div>
    </div>
  </div>
</template>

<script>
import FileDetail from './components/FileDetail'
import TreeItem from './components/TreeItem'

export default {
  name: 'App',
  components: { FileDetail, TreeItem },
  data: function() {
    return {
      files: [],
      filteredFiles: [],
      fields: [],
      distance: null,
      latitude: null,
      longitude: null,
      keyword: null,
      selectedFile: null,
      currentDisc: null,
      startTime: null,
      endTime: null,
      minDate: 0,
      maxDate: 0
    }
  },
  computed: {
    filteredTreeRoots: function() {
      const files = this.filteredFiles
      var treeRoots = {}

      for (var i = 0; i < files.length; i++) {
        let file = files[i]
        const steps = file.r.split(/\//g)
        var current = treeRoots
        const onDisc = file.c === this.currentDisc

        for (var j = 0; j < steps.length; j++) {
          const step = steps[j]
          if (j == (steps.length - 1)) {
            file.name = step
            file.onDisc = onDisc
            current[step] = file
          } else {
            if (!current[step]) current[step] = {isFolder:true, onDisc}
            current = current[step]
            if (onDisc && !current.onDisc) current.onDisc = true
          }
        }
      }

      const arr = this.convertMapToArray(treeRoots)
      return arr
    }
  },
  methods: {
    recomputeFilteredFiles: function() {
      const files = this.files, dstV = parseInt(this.distance), latV = parseFloat(this.latitude), lngV = parseFloat(this.longitude), startV = Date.parse(this.startTime), endV = Date.parse(this.endTime), kw = this.keyword
      const dst = isFinite(dstV) ? dstV : null
      const lat = isFinite(latV) ? latV : null
      const lng = isFinite(lngV) ? lngV : null
      const start = isFinite(startV) && startV > this.minDate && startV < this.maxDate ? startV : null
      const end = isFinite(endV) && endV > this.minDate && endV < this.maxDate ? endV : null
      var valid = []

      if (!(dst && lat && lng) && !kw && !start && !end) return this.addLargeQuantityToArray(files, this.filteredFiles)

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
        if (start && (!file.d || start > file.d)) continue;
        if (end && (!file.d || end < file.d)) continue;
        if (kw &&
          !(
            (file.n && file.n.indexOf(kw) >= 0) ||
            (file.t && file.t.indexOf(kw) >= 0) ||
            (file.x && file.x.indexOf(kw) >= 0) ||
            (file.p && file.p.filter(x => x.indexOf(kw) >= 0).length > 0)
          )) continue;

        valid.push(file);
      }

      this.addLargeQuantityToArray(valid, this.filteredFiles)
    },
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
    },
    convertMapToArray: function(map) {
      const arr = []

      for (var key in map) {
        const value = map[key]
        if (typeof value !== 'object') continue
        if (value.isFolder) arr.push({name:key, children:this.convertMapToArray(value), onDisc:value.onDisc})
        else arr.push(value)
      }

      return arr.sort((l, r) =>
        (l.children && r.children && l.children.length > 0 && r.children.length > 0) || ((!l.children || l.children.length === 0) && (!r.children || r.children.length === 0))
        ? l.name.localeCompare(r.name)
        : (l.children && l.children.length > 0 ? -1 : 1))
    },
    selectFile: function(file) {
      this.selectedFile = file
    },
    addLargeQuantityToArray: function(src, dest) {
      if (src.length < 5000) {
        dest.splice(0, dest.length, ...src)
      } else {
        let start = 0

        dest.splice(0, dest.length)
        while ((start + 5000) < src.length) {
          dest.splice(dest.length, 0, ...src.slice(start, start + 5000))
          start += 5000
        }

        if (start < src.length) {
          dest.splice(dest.length, 0, ...src.slice(start, src.length))
        }
      }
    }
  },
  mounted: function() {
    if (window.archiverMetaData) {
      const metaFiles = window.archiverMetaData.files
      this.addLargeQuantityToArray(metaFiles, this.files)

      var minDate = Number.MAX_SAFE_INTEGER, maxDate = 0
      for (var i = 0; i < metaFiles.length; i++) {
        const file = metaFiles[i]
        if (typeof file.d !== 'number') continue
        if (file.d < minDate) minDate = file.d
        if (file.d > maxDate) maxDate = file.d
      }
      this.minDate = minDate
      this.maxDate = maxDate

      for (var key in window.archiverMetaData.fieldMeaning) {
        this.fields.push({key, value:window.archiverMetaData.fieldMeaning[key]})
      }

      this.recomputeFilteredFiles()
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
  margin-top: 20px;
}

.grid-container {
  display: grid;
  grid-template-columns: 1fr 3fr;
  grid-gap: 20px;
  text-align: left;
  margin-top: 40px;
}

.tree-container {
  max-height: 920px;
  overflow: auto;
}

.item {
  cursor: pointer;
}

ul {
  padding-left: 0;
  line-height: 1.5em;
  list-style-type: none;
}
</style>
