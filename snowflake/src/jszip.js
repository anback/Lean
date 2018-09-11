//@flow
import JSZip from 'jszip'
import JSZipUtils from 'jszip-utils'

new JSZip.external.Promise(function (resolve, reject) {
    JSZipUtils.getBinaryContent('/minute/xbtusd/20180907_trade.zip', function(err, data) {
        if (err) {
            reject(err);
        } else {
            resolve(data);
        }
    });
})
.then(data => JSZip.loadAsync(data))
.then(console.log)
