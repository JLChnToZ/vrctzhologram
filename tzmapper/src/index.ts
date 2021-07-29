import { promisify } from 'util';
import { writeFile } from 'fs';
import { gunzip } from 'zlib';
import JSZip from 'jszip';
import { Feature, feature, truncate, FeatureCollection, union, Polygon, MultiPolygon, pointOnFeature } from '@turf/turf';
import { parse as parseCsv, unparse as unparseCsv } from 'papaparse';
import fetch from 'node-fetch';

const gunzipAsync = promisify(gunzip);
const writeFileAsync = promisify(writeFile);

const GEOJSON_PATH = 'https://github.com/evansiroky/timezone-boundary-builder/releases/download/2020d/timezones.geojson.zip';
const MAPPING_PATH = 'https://github.com/mattjohnsonpint/TimeZoneConverter/raw/main/src/TimeZoneConverter/Data/Mapping.csv.gz';

const zip = new JSZip();
const tzFeatureSet = new Map<string, Feature<Polygon | MultiPolygon>>();
const groupFeatures = new Map<string, string[]>();
const invGroupFeatures = new Map<string, string>();
const result: (string | number)[][] = [];

(async () => {
  console.log('Load timezone boundry data...');
  const geoJsonZip = await zip.loadAsync(await (await fetch(GEOJSON_PATH)).buffer());
  const jsonFile = await geoJsonZip.file('combined.json')?.async('text');
  if (jsonFile == null) throw new Error('Fail to fetch GeoJSON file.');
  const { features } = JSON.parse(jsonFile) as FeatureCollection<Polygon | MultiPolygon>;
  for (const feature of features)
    if (feature.properties?.tzid) {
      console.log(`> ${feature.properties.tzid}`);
      tzFeatureSet.set(feature.properties.tzid, feature);
    }
  console.log('Load Windows mapping data...');
  const mapper = parseCsv<string[]>((await gunzipAsync(await (await fetch(MAPPING_PATH)).buffer())).toString('utf8'));
  for (const row of mapper.data) {
    if (row.length < 3) continue;
    const [netName, , ianaNames] = row;
    const ianaList = ianaNames.split(' ');
    console.log(`> ${netName} -> ${ianaList.length} region(s).`);
    const g = groupFeatures.get(netName);
    if (g) Array.prototype.push.apply(g, ianaList);
    else groupFeatures.set(netName, ianaList);
    for (const ianaName of ianaList) invGroupFeatures.set(ianaName, netName);
  }
  console.log('Merging data...');
  const temp = new Set<string>();
  for (const [group, ianaList] of groupFeatures) {
    console.log(`> ${group}`);
    let unionGeom: Feature<Polygon | MultiPolygon> | undefined;
    for (const ianaName of ianaList) {
      if (temp.has(ianaName) || ianaName.startsWith('Antarctica')) continue;
      temp.add(ianaName);
      console.log(`... ${ianaName}`);
      const f = tzFeatureSet.get(ianaName);
      if (f) unionGeom = unionGeom ? union(unionGeom.geometry, f.geometry) : feature(f.geometry);
    }
    if (unionGeom) tzFeatureSet.set(group, unionGeom);
    temp.clear();
  }
  console.log('Finalize data');
  for (const [tz, feature] of tzFeatureSet) {
    console.log(`> ${tz}`);
    const { geometry: { coordinates: [lon, lat] } } = truncate(pointOnFeature(feature), { precision: 5, coordinates: 2, mutate: true });
    result.push([tz, invGroupFeatures.get(tz) || tz, lon, lat]);
  }
  console.log('> Convert to CSV...');
  await writeFileAsync('tzdata.csv', unparseCsv(result));
  console.log('Finished!');
})().catch(err => console.error(err));
