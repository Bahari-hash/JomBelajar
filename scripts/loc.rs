use clap::Parser;
use ignore::{WalkBuilder, WalkState};
use std::collections::{HashMap, HashSet};
use std::sync::Arc;
use std::sync::Mutex;
use std::thread::available_parallelism;
use std::time::Instant;
use std::{
    ffi::OsString,
    fs::File,
    io::{BufReader, Read},
    path::Path,
};

const BUF_SIZE: usize = 32 * 1024;
const DEFAULT_THREADS_CNT: usize = 4;

#[derive(Parser)]
#[command(version, about)]
struct Args {
    #[arg(short, long, value_delimiter = ',')]
    directories: Vec<String>,

    #[arg(short, long, value_delimiter = ',')]
    extensions: Vec<String>,

    #[arg(short, long, value_delimiter = ',')]
    omit: Vec<String>,
}

fn main() {
    let time = Instant::now();

    let args = Args::parse();
    let dirs = args.directories;
    let exts = Arc::new(
        args.extensions
            .into_iter()
            .map(OsString::from)
            .collect::<HashSet<OsString>>(),
    );
    let omits = Arc::new(
        args.omit
            .into_iter()
            .map(OsString::from)
            .collect::<HashSet<OsString>>(),
    );

    let mut builder = WalkBuilder::from_iter(dirs.iter());
    let ext_counts = Arc::new(Mutex::new(HashMap::<String, usize>::new()));
    // let total_lines = Arc::new(AtomicUsize::from(0));

    let walker = builder
        .threads(
            available_parallelism()
                .map(|n| n.get())
                .unwrap_or(DEFAULT_THREADS_CNT),
        )
        .build_parallel();

    walker.run(|| {
        let exts = Arc::clone(&exts);
        let omits = Arc::clone(&omits);
        let ext_counts = Arc::clone(&ext_counts);

        Box::new(move |result| {
            if let Ok(entry) = result {
                let path = entry.path();
                if !omits.is_empty()
                    && let Some(f) = path.file_name()
                    && omits.contains(f)
                {
                    if path.is_dir() {
                        return WalkState::Skip;
                    } else {
                        return WalkState::Continue;
                    }
                }

                if path.is_file()
                    && let Some(ext) = path.extension()
                    && exts.contains(ext)
                {
                    let ext_string = ext.to_str().unwrap_or("<none>").to_string();
                    let lines = count_lines(path);

                    let mut counts = ext_counts.lock().unwrap();
                    *counts.entry(ext_string).or_insert(0) += lines;
                }
            }
            WalkState::Continue
        })
    });

    let counts = Arc::try_unwrap(ext_counts).unwrap().into_inner().unwrap();
    let mut counts_vec = counts.into_iter().collect::<Vec<(String, usize)>>();
    counts_vec.sort_unstable_by_key(|b| std::cmp::Reverse(b.1));

    let mut total_lines = 0_usize;
    for (ext, line) in counts_vec.into_iter() {
        let ext = format!(".{ext}");
        println!("{ext}: {line} lines");
        total_lines += line;
    }
    println!("total lines: {total_lines}");
    println!("time cost: {:?}", time.elapsed());
}

fn count_lines(path: &Path) -> usize {
    let file = match File::open(path) {
        Ok(f) => f,
        Err(_) => return 0,
    };

    let mut reader = BufReader::with_capacity(BUF_SIZE, file);
    let mut buffer = [0; BUF_SIZE];
    let mut count = 0_usize;
    let mut is_first_chunck = true;
    while let Ok(bytes_read) = reader.read(&mut buffer) {
        if bytes_read == 0 {
            break;
        }

        let chunk = &buffer[..bytes_read];
        // skip binary files such as .exe, .jpg and .etc.
        if is_first_chunck && chunk.contains(&0) {
            return 0;
        }
        is_first_chunck = false;

        count += chunk.iter().filter(|&&b| b == b'\n').count();
    }
    count
}
