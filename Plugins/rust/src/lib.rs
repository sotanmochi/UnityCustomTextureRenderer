// This code is a modified version of the plasma effect code by Keijiro Takahashi.
// https://github.com/keijiro/TextureUpdateExample/blob/master/Plugin/Plasma.c

fn plasma(x: i32, y: i32, width: i32, height: i32, frame: u32) -> u32 {
    let px = (x as f32) / (width as f32);
    let py = (y as f32) / (height as f32);
    let time = (frame as f32) / 60.0;

    let l = f32::sin(px * f32::sin(time * 1.3) + f32::sin(py * 4.0 + time) * f32::sin(time));

    let r = (f32::sin(l *  6.0) * 127.0 + 127.0) as u32;
    let g = (f32::sin(l *  7.0) * 127.0 + 127.0) as u32;
    let b = (f32::sin(l * 10.0) * 127.0 + 127.0) as u32;

    return r + (g << 8) + (b << 16) + 0xff000000;
}

#[no_mangle]
pub unsafe extern "C" fn update_raw_texture_data(buffer: *mut u32, width: i32, height: i32, frame: u32) {
    let data = std::slice::from_raw_parts_mut(buffer, (width * height * 4) as usize);
    for y in 0..height {
        for x in 0..width {
            let index = (y * width + x) as usize;
            data[index] = plasma(x, y, width, height, frame);
        }
    }
}

