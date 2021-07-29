"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const tslib_1 = require("tslib");
const util_1 = require("util");
const fs_1 = require("fs");
const zlib_1 = require("zlib");
const jszip_1 = tslib_1.__importDefault(require("jszip"));
const turf_1 = require("@turf/turf");
const papaparse_1 = require("papaparse");
const node_fetch_1 = tslib_1.__importDefault(require("node-fetch"));
const gunzipAsync = util_1.promisify(zlib_1.gunzip);
const writeFileAsync = util_1.promisify(fs_1.writeFile);
const GEOJSON_PATH = 'https://github.com/evansiroky/timezone-boundary-builder/releases/download/2020d/timezones.geojson.zip';
const MAPPING_PATH = 'https://github.com/mattjohnsonpint/TimeZoneConverter/raw/main/src/TimeZoneConverter/Data/Mapping.csv.gz';
const zip = new jszip_1.default();
const tzFeatureSet = new Map();
const groupFeatures = new Map();
const invGroupFeatures = new Map();
const result = [];
(async () => {
    var _a, _b;
    console.log('Load timezone boundry data...');
    const geoJsonZip = await zip.loadAsync(await (await node_fetch_1.default(GEOJSON_PATH)).buffer());
    const jsonFile = await ((_a = geoJsonZip.file('combined.json')) === null || _a === void 0 ? void 0 : _a.async('text'));
    if (jsonFile == null)
        throw new Error('Fail to fetch GeoJSON file.');
    const { features } = JSON.parse(jsonFile);
    for (const feature of features)
        if ((_b = feature.properties) === null || _b === void 0 ? void 0 : _b.tzid) {
            console.log(`> ${feature.properties.tzid}`);
            tzFeatureSet.set(feature.properties.tzid, feature);
        }
    console.log('Load Windows mapping data...');
    const mapper = papaparse_1.parse((await gunzipAsync(await (await node_fetch_1.default(MAPPING_PATH)).buffer())).toString('utf8'));
    for (const row of mapper.data) {
        if (row.length < 3)
            continue;
        const [netName, , ianaNames] = row;
        const ianaList = ianaNames.split(' ');
        console.log(`> ${netName} -> ${ianaList.length} region(s).`);
        const g = groupFeatures.get(netName);
        if (g)
            Array.prototype.push.apply(g, ianaList);
        else
            groupFeatures.set(netName, ianaList);
        for (const ianaName of ianaList)
            invGroupFeatures.set(ianaName, netName);
    }
    console.log('Merging data...');
    const temp = new Set();
    for (const [group, ianaList] of groupFeatures) {
        console.log(`> ${group}`);
        let unionGeom;
        for (const ianaName of ianaList) {
            if (temp.has(ianaName) || ianaName.startsWith('Antarctica'))
                continue;
            temp.add(ianaName);
            console.log(`... ${ianaName}`);
            const f = tzFeatureSet.get(ianaName);
            if (f)
                unionGeom = unionGeom ? turf_1.union(unionGeom.geometry, f.geometry) : turf_1.feature(f.geometry);
        }
        if (unionGeom)
            tzFeatureSet.set(group, unionGeom);
        temp.clear();
    }
    console.log('Finalize data');
    for (const [tz, feature] of tzFeatureSet) {
        console.log(`> ${tz}`);
        const { geometry: { coordinates: [lon, lat] } } = turf_1.truncate(turf_1.pointOnFeature(feature), { precision: 5, coordinates: 2, mutate: true });
        result.push([tz, invGroupFeatures.get(tz) || tz, lon, lat]);
    }
    console.log('> Convert to CSV...');
    await writeFileAsync('tzdata.csv', papaparse_1.unparse(result));
    console.log('Finished!');
})().catch(err => console.error(err));
//# sourceMappingURL=index.js.map