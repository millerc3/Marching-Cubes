# Marching-Cubes

I recently graduated with a Computer Engineering degree without a job lined up, so in my free time I sought out a neat project to work on and remembered how much I enjoyed learning some Unity basics over the last couple years. I like to use hobby projects to talk about during interviews and I hadn't worked on one in a couple of months so I started this project!

So I made this over the course of the last week with a fair bit of difficulty. Before this project I had only followed a few Brackey's tutorials here and there over the last year, but never really did anything of my own work.  Not only that, but before this project, I didn't even know what a voxel is, what procedural terrain generation is, or the process of mesh creation - safe to say that I learned a whole lot.

After spending probably about 30 hours on it in total, it's not perfect, but my goal was to create a really easy to read and follow - and rather unoptimized - marching cubes project that I want to release for others to learn from. I based this heavily around Paul Burke's work along with the Scrawk repo.

While attempting to learn and understand the algorithm along with c# and unity basics along the way, I really struggled to understand how Scrawk converted the C code to the Unity engine. I have a fair amount of experience with C/C++ so I understood Burke's code, but really struggled with Scrawk's unity code. Maybe this is because I was a novice, and its actually very helpful for experienced Unity devs, but either way I found it difficult to follow.

To help out those who come after me, I decided to create this project with readability in mind. At the cost of a fair bit of optimization, I believe that this code is pretty readable with comments preceding almost every line to explain variables, classes, and functions. In the future I plan to optimize the crap out of this project and hopefully either set up the "Job System" that I keep hearing about, or set up some threads to decrease some of the CPU load.

At the moment, before I added the awful camera movement, the project was running at about 75fps when manipulating the terrain and 200fps at idle. I am pretty happy with this given the amount of array iterating that this project does (and how poor my code structure is).

Thanks for checking it out!



# Known Issues
When attempting to use higher resolutions than "MED", adding to the terrain cannot get higher than y+1 of the clicked voxel
Lag spikes every couple seconds - this seems to be linked to physics processing and I am not sure how to fix it yet
