
//export async function setImage2(imageElementId, imageStream) {


ssswindow.setImageSrc = async (imageElementId, imageStream) => {
    const imageCtrl = document.getElementById(imageElementId);
    console.log(`Here in setImageSrc ${imageElementId} ${imageStream}`);
    if (imageStream === 'null') {
        imageCtrl.src = null;
    }
    else {
        try {
            const arrayBuffer = await imageStream.arrayBuffer();
            const blob = new Blob([arrayBuffer]);
            const url = URL.createObjectURL(blob);
            imageCtrl.onload = () => {
                URL.revokeObjectURL(url);
            }
            imageCtrl.src = url;
        } catch (e) {
            imageCtrl.src = null;
        }
    }
};

